Option Strict On

Imports System.Collections.ObjectModel
Imports CsQuery
Imports System.Text.RegularExpressions
Imports System.Runtime.Serialization.Json
Imports System.Runtime.Serialization

Namespace ViewModels

    <DataContract>
    Public Class BbcViewModel
        Inherits ViewModels.ViewModelBase

        Public Sub New()
            SeasonInfos = New ObservableCollection(Of SeasonNumberUrlInfo)
        End Sub

#Region " Properties "

        Private _showName As String
        <DataMember>
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _imageSize As String = "1920x1080"
        <DataMember>
        Public Property ImageSize As String
            Get
                Return _imageSize
            End Get
            Set(value As String)
                SetProperty(_imageSize, value)
            End Set
        End Property

        Private _seasonInfos As ObservableCollection(Of SeasonNumberUrlInfo)
        <DataMember>
        Public Property SeasonInfos As ObservableCollection(Of SeasonNumberUrlInfo)
            Get
                Return _seasonInfos
            End Get
            Set(value As ObservableCollection(Of SeasonNumberUrlInfo))
                SetProperty(_seasonInfos, value)
            End Set
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                If Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe,
                                           "Season " & CInt(SeasonNumber).ToString("00"))
                Else
                    Return String.Empty
                End If
            End Get
        End Property

        Public ReadOnly Property ShowDownloadFolder As String
            Get
                If Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe)
                Else
                    Return String.Empty
                End If
            End Get
        End Property

        Private _noEpisodeNumbers As Boolean
        <DataMember>
        Public Property NoEpisodeNumbers As Boolean
            Get
                Return _noEpisodeNumbers
            End Get
            Set(value As Boolean)
                SetProperty(_noEpisodeNumbers, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return SeasonInfos.Count > 0 AndAlso
                   SeasonInfosValid() AndAlso
                   Not String.IsNullOrEmpty(ShowName) AndAlso
                   ImageSizeValid()
        End Function

        Private Function SeasonInfosValid() As Boolean
            Return SeasonInfos.Where(Function(e) Not String.IsNullOrWhiteSpace(e.Url) AndAlso e.Number.HasValue).Count() = SeasonInfos.Count()
            'Dim returnValue As Boolean = True
            'For Each info In EpisodeInfos
            '    If info.EpisodeNumber.HasValue AndAlso Not String.IsNullOrEmpty(info.EpisodeUrl) Then
            '        Return True
            '    End If
            'Next
            'Return False
        End Function

        Private Function ImageSizeValid() As Boolean
            Return ImageSize.ToLower() = "none" OrElse Regex.IsMatch(ImageSize, "^\d+x\d+$")
        End Function

        'Public Sub Test(seriesName As String, seasonInfoCollection As ObservableCollection(Of SeasonInfo))
        '    ShowName = seriesName
        '    SeasonInfos = seasonInfoCollection
        '    DownloadImages()
        'End Sub

        Protected Overrides Sub DownloadImages()
            ''Parallel.ForEach(EpisodeInfos, New ParallelOptions() With {.MaxDegreeOfParallelism = 6}, AddressOf DownloadEpisodeImages)

            For Each SeasonInfo In SeasonInfos
                DownloadEpisodeImages(SeasonInfo)
            Next
        End Sub

        Private Sub DownloadEpisodeImages(info As SeasonNumberUrlInfo)
            If info.Number.HasValue Then
                Dim tvData As New TvDataSeries()
                Dim cqDoc As CQ
                Dim guideUrl = info.Url
                'Dim guideUrlHost As String = New Uri(guideUrl).Host
                Dim guideUrlSchemeAndServer As String = New Uri(guideUrl).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped)
                Do
                    Dim html As String
                    Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                        html = client.DownloadString(guideUrl)
                    End Using
                    cqDoc = CQ.Create(html)

                    If Not NoEpisodeNumbers Then
                        'cqDoc("li.episode").Each(Sub(domObj As IDomObject)
                        '                             ParseEpisodeListItem(domObj, info, tvData)
                        '                         End Sub)

                        Parallel.ForEach(cqDoc("li.episode").ToList(), Sub(domObj As IDomObject)
                                                                           ParseEpisodeListItem(domObj, info, tvData)
                                                                       End Sub)
                    Else
                        cqDoc("li.episode").Each(Sub(index As Integer, domObj As IDomObject)
                                                     ParseEpisodeSequentially(domObj, info, tvData, index, cqDoc("li.episode").Length)
                                                 End Sub)
                    End If

                    guideUrl = cqDoc("a[rel=""next""]").Attr("href")
                    If guideUrl IsNot Nothing Then
                        Dim bbcUri As New Uri(guideUrl, UriKind.RelativeOrAbsolute)
                        If Not bbcUri.IsAbsoluteUri Then
                            bbcUri = New Uri(New Uri(guideUrlSchemeAndServer), bbcUri.ToString())
                            guideUrl = bbcUri.ToString()
                        End If
                    End If

                Loop Until guideUrl Is Nothing

                If tvData.Episodes.Count > 0 Then
                    tvData.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & info.Number.Value.ToString("00"))
                Else
                    MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
                End If

            End If
        End Sub

        Private Sub ParseEpisodeListItem(domObj As IDomObject, info As SeasonNumberUrlInfo, ByRef TvData As TvDataSeries)
            Dim div As CsQuery.CQ = domObj.Cq()
            Try
                Dim bbcEpisode As New TvDataEpisode() With {
                                .SeasonNumber = info.Number.Value,
                                .EpisodeNumber = CInt(div.Find("span[property=""po:position""]").Text()),
                                .EpisodeName = div.Find("span.title").Text(),
                                .Overview = div.Find("span[property=""po:short_synopsis""]").Text(),
                                .FirstAired = div.Find(".first-broadcast").Contents().Filter(Function(d) d.NodeType = NodeType.TEXT_NODE).Text().Trim().Trim(CChar(":")).ToIso8601DateString}
                TvData.Episodes.Add(bbcEpisode)
                If ImageSize.ToLower() = "none" Then
                    Dim SeasonEpisodeFilename = "S" & info.Number.Value.ToString("00") & "E" & bbcEpisode.EpisodeNumber.ToString("00") & "-" & bbcEpisode.EpisodeName.MakeFileNameSafe()
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Parsed but download skipped."})
                Else
                    DownloadImage(div.Find("[rel=""foaf:depiction""] img").Attr("src"), bbcEpisode, info.Number.Value)
                End If

                'DownloadImage(div.Find(".lazy-img").Attr("data-img-src-68"), bbcEpisode, info.Number.Value)
            Catch ex As Exception

            End Try
        End Sub

        Private Sub ParseEpisodeSequentially(domObj As IDomObject, info As SeasonNumberUrlInfo, ByRef TvData As TvDataSeries, index As Integer, totalCount As Integer)
            Dim div As CsQuery.CQ = domObj.Cq()
            Try
                Dim bbcEpisode As New TvDataEpisode() With {
                                .SeasonNumber = info.Number.Value,
                                .EpisodeNumber = totalCount - index,
                                .EpisodeName = div.Find("span.title").Text(),
                                .Overview = div.Find("span[property=""po:short_synopsis""]").Text(),
                                .FirstAired = div.Find(".first-broadcast").Contents().Filter(Function(d) d.NodeType = NodeType.TEXT_NODE).Text().Trim().Trim(CChar(":")).ToIso8601DateString}
                TvData.Episodes.Add(bbcEpisode)
                If ImageSize.ToLower() = "none" Then
                    Dim SeasonEpisodeFilename = "S" & info.Number.Value.ToString("00") & "E" & bbcEpisode.EpisodeNumber.ToString("00") & "-" & bbcEpisode.EpisodeName.MakeFileNameSafe()
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Parsed but download skipped."})
                Else
                    DownloadImage(div.Find("[rel=""foaf:depiction""] img").Attr("src"), bbcEpisode, info.Number.Value)
                End If
            Catch ex As Exception

            End Try
        End Sub

        Private Sub DownloadImage(imageUrl As String, episode As TvDataEpisode, SeasonNumber As Integer)
            'If Not IO.Directory.Exists(SeasonDownloadFolder(SeasonNumber)) Then
            '    IO.Directory.CreateDirectory(SeasonDownloadFolder(SeasonNumber))
            'End If

            ''Dim Filename = IO.Path.GetFileNameWithoutExtension(imageUrl)
            Dim extension = IO.Path.GetExtension(imageUrl)
            Dim SeasonEpisodeFilename = "S" & SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.EpisodeName.MakeFileNameSafe() & "_" & ImageSize & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(imageUrl, localPath)

            'If Not IO.File.Exists(localPath) Then
            '    Try
            '        Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
            '            Dim fullSizeUrl = GetFullSizeImageUrl(imageUrl)
            '            client.DownloadFile(fullSizeUrl, localPath)
            '        End Using
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If
        End Sub

        Private Function GetFullSizeImageUrl(originalUrl As String) As String
            Return Regex.Replace(originalUrl, "\/\d+x\d+\/", "/" & ImageSize & "/")
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder)
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Public Function SeasonInfoFileExists() As Boolean
            Return IO.File.Exists(SeasonInfoFileName)
        End Function

        Public Function SeasonInfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName & ".eid")
        End Function

        Public ReadOnly Property SaveSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           If Not IO.Directory.Exists(ShowDownloadFolder) Then
                                                               IO.Directory.CreateDirectory(ShowDownloadFolder)
                                                           End If
                                                           Using siFile As IO.FileStream = IO.File.OpenWrite(SeasonInfoFileName)
                                                               Dim jsonSerializer As New DataContractJsonSerializer(GetType(BbcViewModel))
                                                               jsonSerializer.WriteObject(siFile, Me)
                                                           End Using
                                                       End Sub,
                                                       Function() As Boolean
                                                           Return Not String.IsNullOrWhiteSpace(ShowName)
                                                       End Function)
            End Get
        End Property

        Public ReadOnly Property LoadSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Dim bbcVM As New BbcViewModel
                                                           Using stream As New IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                                                               Dim jsonSerializer As New DataContractJsonSerializer(GetType(BbcViewModel))
                                                               bbcVM = CType(jsonSerializer.ReadObject(stream), BbcViewModel)
                                                           End Using
                                                           SeasonInfos = bbcVM.SeasonInfos
                                                           NoEpisodeNumbers = bbcVM.NoEpisodeNumbers
                                                           ImageSize = bbcVM.ImageSize
                                                       End Sub, AddressOf SeasonInfoFileExists)
            End Get
        End Property

        Public Shared ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"1920x1080", "1280x720", "960x540", "None"})
            End Get
        End Property

    End Class

End Namespace
