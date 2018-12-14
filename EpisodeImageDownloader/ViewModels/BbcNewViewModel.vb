Option Strict On

Imports System.Collections.ObjectModel
Imports CsQuery
Imports System.Text.RegularExpressions
Imports System.Runtime.Serialization.Json
Imports System.Runtime.Serialization

Namespace ViewModels

    <DataContract>
    Public Class BbcNewViewModel
        Inherits BbcBaseViewModel

        Public Sub New()
            SeasonInfos = New ObservableCollection(Of SeasonNumberUrlInfo)
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 6, .Url = "http://www.bbc.co.uk/programmes/b03bgntx/episodes/guide"})
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 5, .Url = "http://www.bbc.co.uk/programmes/b015frjh/episodes/guide"})
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 4, .Url = "http://www.bbc.co.uk/programmes/b00pchgg/episodes/guide"})
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 3, .Url = "http://www.bbc.co.uk/programmes/b00mwcrc/episodes/guide"})
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 2, .Url = "http://www.bbc.co.uk/programmes/b007zqjp/episodes/guide"})
            'SeasonInfos.Add(New SeasonInfo() With {.Number = 1, .Url = "http://www.bbc.co.uk/programmes/b00gtlkn/episodes/guide"})
        End Sub

#Region " Properties "

        Private _noEpisodeNumbers As Boolean
        <DataMember(Order:=3, EmitDefaultValue:=False), ComponentModel.DefaultValue(False)>
        Public Property NoEpisodeNumbers As Boolean
            Get
                Return _noEpisodeNumbers
            End Get
            Set(value As Boolean)
                SetProperty(_noEpisodeNumbers, value)
            End Set
        End Property

        Private _getFirstAiredEpisodeDates As Boolean = True
        <DataMember(Order:=4, EmitDefaultValue:=False), ComponentModel.DefaultValue(True)>
        Public Property GetFirstAiredEpisodeDates As Boolean
            Get
                Return _getFirstAiredEpisodeDates
            End Get
            Set(value As Boolean)
                SetProperty(_getFirstAiredEpisodeDates, value)
            End Set
        End Property

        Private _getExtendedOverview As Boolean = False
        <DataMember(Order:=5, EmitDefaultValue:=False), ComponentModel.DefaultValue(False)>
        Public Property GetExtendedOverview As Boolean
            Get
                Return _getExtendedOverview
            End Get
            Set(value As Boolean)
                SetProperty(_getExtendedOverview, value)
            End Set
        End Property

        Private _seasonInfos As ObservableCollection(Of SeasonNumberUrlInfo)
        <DataMember(Order:=6)>
        Public Property SeasonInfos As ObservableCollection(Of SeasonNumberUrlInfo)
            Get
                Return _seasonInfos
            End Get
            Set(value As ObservableCollection(Of SeasonNumberUrlInfo))
                SetProperty(_seasonInfos, value)
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
            Return SeasonInfos.Where(Function(e) Not String.IsNullOrWhiteSpace(e.Url) AndAlso
                                                 e.Number.HasValue).Count() = SeasonInfos.Count()
            'Dim returnValue As Boolean = True
            'For Each info In EpisodeInfos
            '    If info.EpisodeNumber.HasValue AndAlso Not String.IsNullOrEmpty(info.EpisodeUrl) Then
            '        Return True
            '    End If
            'Next
            'Return False
        End Function

        'Public Sub Test(seriesName As String, seasonInfoCollection As ObservableCollection(Of SeasonInfo))
        '    ShowName = seriesName
        '    SeasonInfos = seasonInfoCollection
        '    DownloadImages()
        'End Sub

        Protected Overrides Sub DownloadImages()
            Parallel.ForEach(SeasonInfos, AddressOf DownloadSeasonImages)

            'For Each SeasonInfo In SeasonInfos
            '    DownloadSeasonImages(SeasonInfo)
            'Next
        End Sub

        Private Sub DownloadSeasonImages(info As SeasonNumberUrlInfo)
            If info.Number.HasValue Then
                Dim tvData As New TvDataSeries()
                Dim cqDoc As CQ
                Dim guideUrl = info.Url
                'Dim guideUrlHost As String = New Uri(guideUrl).Host
                Dim enteredUri = New Uri(guideUrl, UriKind.Absolute)
                Dim baseUri As Uri = New Uri(enteredUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped))
                Dim guideItems As New List(Of IDomObject)
                Do
                    Dim html As String = Infrastructure.WebResources.DownloadString(guideUrl)
                    ''Dim html As String = Infrastructure.WebResources.DownloadString("https://www.bbc.co.uk/programmes/b07hjq20/episodes/guide.inc")
                    'Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                    '    html = client.DownloadString(guideUrl)
                    'End Using
                    cqDoc = CQ.Create(html)
                    guideItems.AddRange(cqDoc("div.js-guideitem").ToList())


                    guideUrl = cqDoc(".pagination__next a").Attr("href")
                    If guideUrl IsNot Nothing Then
                        Dim bbcUri As New Uri(guideUrl, UriKind.RelativeOrAbsolute)
                        If Not bbcUri.IsAbsoluteUri Then
                            bbcUri = New Uri(baseUri, bbcUri.ToString())
                            guideUrl = bbcUri.ToString()
                        End If
                    End If
                Loop Until guideUrl Is Nothing


                If Not NoEpisodeNumbers Then
                    'guideItems.ForEach(Sub(domObj As IDomObject)
                    '                       ParseEpisodeListItem(domObj, info, tvData, baseUri)
                    '                   End Sub)

                    Parallel.ForEach(guideItems, Sub(domObj As IDomObject)
                                                     ParseEpisodeListItem(domObj, info, tvData, baseUri)
                                                 End Sub)
                Else
                    cqDoc("div.js-guideitem").Each(Sub(index As Integer, domObj As IDomObject)
                                                       ParseEpisodeSequentially(domObj, info, tvData, index, cqDoc("div.js-guideitem").Length, baseUri)
                                                   End Sub)

                    'Dim index As Integer = 0
                    'For Each guideItem In guideItems
                    '    ParseEpisodeSequentially(guideItem, info, tvData, index, guideItems.Count, baseUri)
                    '    index += 1
                    'Next

                End If


                If tvData.Episodes.Count > 0 Then
                    tvData.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & info.Number.Value.ToString("00"))
                Else
                    MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
                End If

            End If
        End Sub

        Private Sub ParseEpisodeListItem(domObj As IDomObject, info As SeasonNumberUrlInfo, ByRef TvData As TvDataSeries, baseUri As Uri)
            Dim div As CQ = domObj.Cq()
            Try
                Dim bbcEpisode As New TvDataEpisode() With {
                                .SeasonNumber = info.Number.Value,
                                .EpisodeNumber = CInt(div.Find(".programme__synopsis abbr span:first").Text()),
                                .EpisodeName = div.Find(".programme__title span:first").Text(),
                                .Overview = div.Find(".programme__synopsis span:last").Text()}
                If GetFirstAiredEpisodeDates Then
                    bbcEpisode.FirstAired = GetEpisodeFirstAired(GetEpisodeBroadcastsUri(div, baseUri))
                End If

                If GetFirstAiredEpisodeDates Or GetExtendedOverview Then

                End If


                TvData.Episodes.Add(bbcEpisode)
                If ImageSize.ToLower() = "none" Then
                    Dim SeasonEpisodeFilename = "S" & info.Number.Value.ToString("00") & "E" & bbcEpisode.EpisodeNumber.ToString("00") & "-" & bbcEpisode.EpisodeName.MakeFileNameSafe()
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Parsed but download skipped."})
                Else
                    DownloadImage(div.Find("img").First().Attr("data-src"), bbcEpisode)
                End If

                'DownloadImage(div.Find(".lazy-img").Attr("data-img-src-68"), bbcEpisode)
            Catch ex As Exception

            End Try
        End Sub

        Private Sub ParseEpisodeSequentially(domObj As IDomObject, info As SeasonNumberUrlInfo, ByRef TvData As TvDataSeries, index As Integer, totalCount As Integer, baseUri As Uri)
            Dim div As CQ = domObj.Cq()
            Try
                Dim bbcEpisode As New TvDataEpisode() With {
                                .SeasonNumber = info.Number.Value,
                                .EpisodeNumber = totalCount - index,
                                .EpisodeName = div.Find("span[property=name]:first").Text(),
                                .Overview = div.Find("span[property=""description""]").Text()}
                '.FirstAired = div.Find(".first-broadcast").Contents().Filter(Function(d) d.NodeType = NodeType.TEXT_NODE).Text().Trim().Trim(CChar(":")).ToIso8601DateString}
                If GetFirstAiredEpisodeDates Then
                    bbcEpisode.FirstAired = GetEpisodeFirstAired(GetEpisodeBroadcastsUri(div, baseUri))
                End If
                TvData.Episodes.Add(bbcEpisode)
                If ImageSize.ToLower() = "none" Then
                    Dim SeasonEpisodeFilename = "S" & info.Number.Value.ToString("00") & "E" & bbcEpisode.EpisodeNumber.ToString("00") & "-" & bbcEpisode.EpisodeName.MakeFileNameSafe()
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Parsed but download skipped."})
                Else
                    DownloadImage(div.Find("meta[property=""image""]").Attr("content"), bbcEpisode)
                End If
            Catch ex As Exception

            End Try
        End Sub

        Private Sub DownloadImage(imageUrl As String, episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(imageUrl)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" &
                episode.EpisodeName.MakeFileNameSafe() & "_" & ImageSize & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(GetFullSizeImageUrl(imageUrl), localPath)
        End Sub

#Region " Save/Load Season Infos "

        Public Function SeasonInfoFileExists() As Boolean
            Return IO.File.Exists(SeasonInfoFileName)
        End Function

        Public Function SeasonInfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName.MakeFileNameSafe & ".eid")
        End Function

        Public ReadOnly Property SaveSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(ShowDownloadFolder) Then
                            IO.Directory.CreateDirectory(ShowDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(SeasonInfoFileName) Then
                            System.IO.File.WriteAllText(SeasonInfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(SeasonInfoFileName)
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(BbcNewViewModel))
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse CanDownloadImages()
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadSeasonInfoCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Dim bbcVM As New BbcNewViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(BbcNewViewModel))
                            bbcVM = CType(jsonSerializer.ReadObject(stream), BbcNewViewModel)
                        End Using
                        SeasonInfos = bbcVM.SeasonInfos
                        NoEpisodeNumbers = bbcVM.NoEpisodeNumbers
                        GetFirstAiredEpisodeDates = bbcVM.GetFirstAiredEpisodeDates
                        GetExtendedOverview = bbcVM.GetExtendedOverview
                        ImageSize = bbcVM.ImageSize
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse SeasonInfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

    End Class

End Namespace
