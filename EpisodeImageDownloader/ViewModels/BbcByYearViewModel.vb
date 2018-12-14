Option Strict On
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.Serialization
Imports CsQuery

Namespace ViewModels

    <DataContract>
    Public Class BbcByYearViewModel
        Inherits BbcBaseViewModel
        Implements INotifyDataErrorInfo

#Region " Properties "

        Private _url As String
        <DataMember(Order:=3)>
        Public Property Url As String
            Get
                Return _url
            End Get
            Set(value As String)
                SetProperty(_url, value)
                Validate()
            End Set
        End Property

        Private _year As Integer = 2017
        <DataMember(Order:=4)>
        Public Property Year As Integer
            Get
                Return _year
            End Get
            Set(value As Integer)
                SetProperty(_year, value)
                Validate()
            End Set
        End Property

        Private _season As Integer?
        <DataMember(Order:=5)>
        Public Property Season As Integer?
            Get
                Return _season
            End Get
            Set(value As Integer?)
                SetProperty(_season, value)
            End Set
        End Property

        'Private _startPageNumber As Integer? = 1
        'Public Property StartPageNumber As Integer?
        '    Get
        '        Return _startPageNumber
        '    End Get
        '    Set(value As Integer?)
        '        SetProperty(_startPageNumber, value)
        '    End Set
        'End Property

        Private _statusMessage As String
        Public Property StatusMessage As String
            Get
                Return _statusMessage
            End Get
            Set(value As String)
                SetProperty(_statusMessage, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return UrlIsValid() AndAlso
                   Not String.IsNullOrEmpty(ShowName) AndAlso
                   ImageSizeValid() AndAlso
                   Year.ToString.Length = 4
        End Function

        Private Function UrlIsValid() As Boolean
            Try
                Dim uri = New Uri(Url)
            Catch ex As Exception
                Return False
            End Try
            Return True
        End Function

        Protected Overrides Sub DownloadImages()
            Dim tvData As New TvDataSeries()
            Dim cqDoc As CQ
            Dim guideUrl = Url
            Dim enteredUri = New Uri(Url, UriKind.Absolute)
            Dim baseUri As Uri = New Uri(enteredUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped))
            Dim foundEndOfYear As Boolean = False
            'parse the first page adding guideItems to the list and continue to subsequent pages 
            ' if available and the last episode has a first aired date with year in the list of YearToSeasons
            Do
                StatusMessage = "Downloading " & guideUrl
                Dim html As String = Infrastructure.WebResources.DownloadString(guideUrl)
                cqDoc = CQ.Create(html)
                Dim guideItems = cqDoc("div.js-guideitem")
                guideItems.Each(
                    Sub(i As Integer, iDom As IDomObject)
                        If Not foundEndOfYear Then
                            StatusMessage = String.Format("Page {0}: Episode {1} of {2}", GetPageNumber(guideUrl), i + 1, guideItems.Count)
                            Dim tvEp = ParseEpisodeListItem(iDom, baseUri)
                            If tvEp.FirstAiredYear = Year Then
                                tvData.Episodes.Add(tvEp)
                            End If
                            foundEndOfYear = CBool(tvEp.FirstAiredYear < Year)
                        End If
                    End Sub)

                guideUrl = cqDoc(".pagination__next a").Attr("href")
                If guideUrl IsNot Nothing Then
                    Dim bbcUri As New Uri(guideUrl, UriKind.RelativeOrAbsolute)
                    If Not bbcUri.IsAbsoluteUri Then
                        bbcUri = New Uri(baseUri, bbcUri.ToString())
                        guideUrl = bbcUri.ToString()
                    End If
                End If

            Loop Until guideUrl Is Nothing Or foundEndOfYear

            'assign episode number based on position in list
            Dim index As Integer = 0
            For Each ep In tvData.Episodes
                ep.EpisodeNumber = tvData.Episodes.Count - index
                index += 1
            Next

            'download the images
            If ImageSize <> "None" Then
                StatusMessage = "Downloading images"
                Parallel.ForEach(tvData.Episodes, AddressOf DownloadImage)
            End If

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName & " Season " & EffectiveSeasonNumber())
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If
            StatusMessage = "Done!"
            If ImageSize = "None" Then
                StatusMessage &= " (skipped image download, imageSize = None)"
            End If
        End Sub

        Private Function GetPageNumber(guideUrl As String) As Integer
            Dim rx As New Text.RegularExpressions.Regex("\?page=(\d+)", Text.RegularExpressions.RegexOptions.IgnoreCase)
            Dim pageMatch = rx.Match(guideUrl)
            If pageMatch.Success Then
                Return CInt(pageMatch.Groups(1).Value)
            End If
            Return 1
        End Function

        Private Function ParseEpisodeListItem(domObj As IDomObject,
                                         baseUri As Uri) As TvDataEpisode
            Dim div As CQ = domObj.Cq()
            Dim bbcEpisode As New TvDataEpisode
            Try
                bbcEpisode = New TvDataEpisode() With {
                                .SeasonNumber = EffectiveSeasonNumber(),
                                .EpisodeName = div.Find("span[property=name]:first").Text(),
                                .Overview = div.Find("span[property=""description""]").Text(),
                                .ImageUrl = div.Find("meta[property=""image""]").Attr("content"),
                                .FirstAired = GetEpisodeFirstAired(GetEpisodeBroadcastsUri(div, baseUri))}
            Catch ex As Exception
            End Try
            Return bbcEpisode
        End Function

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" &
                episode.EpisodeName.MakeFileNameSafe() & "_" & ImageSize & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(GetFullSizeImageUrl(episode.ImageUrl), localPath)
        End Sub

        Private Function EffectiveSeasonNumber() As Integer
            Return If(Season, Year)
        End Function

#Region " Save/Load Info "

        Public Function InfoFileExists() As Boolean
            Return IO.File.Exists(InfoFileName)
        End Function

        Public Function InfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName.MakeFileNameSafe & ".eid")
        End Function

        Public ReadOnly Property SaveInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(ShowDownloadFolder) Then
                            IO.Directory.CreateDirectory(ShowDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(InfoFileName) Then
                            IO.File.WriteAllText(InfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(InfoFileName)
                            Dim jsonSerializer As New Json.DataContractJsonSerializer(Me.GetType())
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        Return DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse CanDownloadImages()
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Dim bbcVM As New BbcByYearViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(InfoFileName)))
                            Dim jsonSerializer As New Json.DataContractJsonSerializer(Me.GetType())
                            bbcVM = CType(jsonSerializer.ReadObject(stream), BbcByYearViewModel)
                        End Using
                        ImageSize = bbcVM.ImageSize
                        Url = bbcVM.Url
                        Year = bbcVM.Year
                        Season = bbcVM.Season
                    End Sub,
                    Function() As Boolean
                        Return DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse InfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

#Region " INotifyDataErrorInfo Members "

        ''' <summary>
        ''' Force the object to validate itself using the assigned business rules.
        ''' </summary>
        ''' <param name="propertyName">Name of the property to validate.</param>
        Protected Overrides Sub Validate(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
            MyBase.Validate(propertyName)
            If String.IsNullOrEmpty(propertyName) Then
                Return
            End If

            Select Case propertyName
                Case "Url"
                    Try
                        Dim uri = New Uri(Url)
                        ClearError(propertyName)
                    Catch ex As Exception
                        AddError(propertyName, ex.Message)
                    End Try
                Case = "Year"
                    If Year.ToString.Length <> 4 Then
                        AddError(propertyName, "Year should be a 4 digit number")
                    Else
                        ClearError(propertyName)
                    End If
            End Select
        End Sub

#End Region

    End Class

End Namespace
