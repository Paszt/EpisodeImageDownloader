Option Strict On

Imports System.Collections.ObjectModel
Imports CsQuery
Imports System.Text.RegularExpressions

Namespace ViewModels

    Public Class DisneyAbcPressViewModel
        Inherits ViewModels.ViewModelBase

        Public Sub New()
            EpisodeInfos = New ObservableCollection(Of EpisodeInformation)
        End Sub

#Region " Properties "

        Private _showName As String
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _channel As String = "abc"
        Public Property Channel As String
            Get
                Return _channel
            End Get
            Set(value As String)
                SetProperty(_channel, value)
            End Set
        End Property

        Private _episodeListUrl As String
        Public Property EpisodeListUrl As String
            Get
                Return _episodeListUrl
            End Get
            Set(value As String)
                ' update channel based on URL
                Dim elUri As Uri = Nothing
                If Uri.TryCreate(value, UriKind.Absolute, elUri) Then
                    Dim uriPath = elUri.GetComponents(UriComponents.Path, UriFormat.UriEscaped)
                    For Each ch As ChannelItem In ChannelEnumeration
                        If uriPath.StartsWith(ch.Slug) Then
                            Channel = ch.Slug
                        End If
                    Next
                End If

                SetProperty(_episodeListUrl, value)
            End Set
        End Property

        Private _episodeInfos As ObservableCollection(Of EpisodeInformation)
        Public Property EpisodeInfos As ObservableCollection(Of EpisodeInformation)
            Get
                Return _episodeInfos
            End Get
            Set(value As ObservableCollection(Of EpisodeInformation))
                SetProperty(_episodeInfos, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return EpisodeInfos.Count > 0 AndAlso
                   Not String.IsNullOrEmpty(ShowName)
        End Function

        Protected Overrides Sub DownloadImages()
            'Parallel.ForEach(EpisodeInfos, AddressOf DownloadEpisodeImages)
            For Each ei In EpisodeInfos
                DownloadEpisodeImages(ei)
            Next
        End Sub

        Private Sub DownloadEpisodeImages(ei As EpisodeInformation)
            If Not ei.EpisodeId.HasValue Then
                Exit Sub
            End If
            Dim urls As New List(Of String)
            Using _client As New Infrastructure.ChromeWebClient()
                Dim numberOfImages As Integer = 0
                Dim pageNumber As Integer = 1
                Do
                    Dim doc = CQ.Create(_client.DownloadString(ImagePageUrl(CInt(ei.EpisodeId), pageNumber)))
                    numberOfImages = doc("img").Length()
                    doc("img").Each(Sub(img As IDomObject)
                                        Dim imgSrc = img.Cq.Attr("src")
                                        If Not urls.Contains(imgSrc) AndAlso Not String.IsNullOrWhiteSpace(imgSrc) Then
                                            urls.Add(imgSrc)
                                        End If
                                    End Sub)
                    pageNumber += 1
                Loop Until numberOfImages = 0
            End Using

            Parallel.ForEach(urls, New ParallelOptions() With {.MaxDegreeOfParallelism = 4},
                             Sub(url As String)
                                 DownloadImage(url, ei)
                             End Sub)
            'For Each url In urls
            '    DownloadImage(url, ei)
            'Next
        End Sub

        'Private Sub DownloadImage(imageUrl As String, ei As EpisodeInformation)
        '    Dim downloadFolder = EpisodeDownloadFolder(CInt(ei.SeasonNumber), CInt(ei.EpisodeNumber))
        '    imageUrl = Regex.Replace(imageUrl, "-\d+x\d+", String.Empty)
        '    Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
        '    Filename = "S" & ei.SeasonNumber.Value.ToString("00") & "E" & ei.EpisodeNumber.Value.ToString("000") & "_" & _
        '        ei.EpisodeTitle.MakeSafeFileNameNoSpaces() & "_" & Filename
        '    Dim localPath = IO.Path.Combine(downloadFolder, Filename)
        '    MyBase.DownloadImageAddResult(imageUrl, localPath)
        'End Sub

        Private Sub DownloadImage(imageUrl As String, ei As EpisodeInformation)
            Dim downloadFolder = EpisodeDownloadFolder(CInt(ei.SeasonNumber), CInt(ei.EpisodeNumber))
            If Not IO.Directory.Exists(downloadFolder) Then
                IO.Directory.CreateDirectory(downloadFolder)
            End If
            imageUrl = Regex.Replace(imageUrl, "-\d+x\d+", String.Empty)
            Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
            Filename = "S" & ei.SeasonNumber.Value.ToString("00") & "E" & ei.EpisodeNumber.Value.ToString("000") & "_" & _
                ei.EpisodeTitle.MakeFileNameSafeNoSpaces() & "_" & Filename
            Dim localPath = IO.Path.Combine(downloadFolder, Filename)

            If Not IO.File.Exists(localPath) Then
                Try
                    Using _client As New Infrastructure.ChromeWebClient() With {.Referer = ei.EpisodePhotosUrl}
                        _client.DownloadFile(imageUrl, localPath)
                    End Using
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                Catch ex As Exception
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                End Try
            Else
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            End If
        End Sub

        Private Function ImagePageUrl(episodeId As Integer, pageNumber As Integer) As String
            ' {0} = channel
            ' {1} = episode Id
            ' {2} = page number
            ' {3} = date epoch
            Dim imageUrlFormat = "http://www.disneyabcpress.com/{0}/wp-admin/admin-ajax.php?action=dap_drawer&id={1}&slug=Episodic&type=gallery-more-photos&target=wrap_category_Episodic&post_type=episode&page={2}&date={3}"
            Return String.Format(imageUrlFormat, Channel, episodeId, pageNumber, CInt((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalSeconds))
        End Function

#Region " Download & Parse Episode Information "

        Private Sub GetEpisodeInformation()
            Dim html = Infrastructure.WebResources.DownloadString(EpisodeListUrl)
            Dim doc = CQ.Create(html)
            EpisodeInfos.Clear()
            doc("ul.schedule-list-horiz-med-tall li").Each(Sub(li As idomobject)
                                                               EpisodeInfos.Add(ParseEpisodeInfo(li))
                                                           End Sub)
        End Sub

        Private Function ParseEpisodeInfo(li As IDomObject) As EpisodeInformation
            Dim ei As New EpisodeInformation()
            Dim id As Integer
            If Integer.TryParse(li.Cq.Find("a.add-item").Attr("data-item-id"), id) Then
                ei.EpisodeId = id
            End If
            Dim seasonEpisode = li.Cq.Find(".inner").Text()
            Dim seMatch = Regex.Match(seasonEpisode, "(?<season>\d+)(?<episode>\d{2})")
            If seMatch.Success Then
                ei.SeasonNumber = CInt(seMatch.Groups("season").Value)
                ei.EpisodeNumber = CInt(seMatch.Groups("episode").Value)
            End If
            ei.EpisodeTitle = li.Cq.Find(".schedule-details a[title]").Attr("title").Trim()
            ei.EpisodePhotosUrl = li.Cq.Find("a:contains(Episode Photos)").Attr("href")
            Return ei
        End Function

        Public ReadOnly Property GetEpisodeInformationCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(AddressOf GetEpisodeInformation,
                                                       Function()
                                                           Return Not String.IsNullOrWhiteSpace(EpisodeListUrl)
                                                       End Function)
            End Get
        End Property

#End Region

#Region " Folder Functions "

        Public Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe())
            Else
                Return String.Empty
            End If
        End Function

        Public Function SeasonDownloadFolder(SeasonNumber As Integer) As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(ShowDownloadFolder(),
                                       "Season " & SeasonNumber.ToString("00"))
            Else
                Return String.Empty
            End If
        End Function

        Public Function EpisodeDownloadFolder(SeasonNumber As Integer, EpisodeNumber As Integer) As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(SeasonDownloadFolder(SeasonNumber),
                                       "S" & SeasonNumber.ToString("00") & "E" & CInt(EpisodeNumber).ToString("00"))
            Else
                Return String.Empty
            End If
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder())
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder())
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

#End Region

#Region " Channel Enums & values "

        Public ReadOnly Property ChannelEnumeration As List(Of ChannelItem) = New List(Of ChannelItem) From {
                New ChannelItem() With {.Name = "ABC", .Slug = "abc"},
                New ChannelItem() With {.Name = "ABC Family", .Slug = "abcfamily"},
                New ChannelItem() With {.Name = "Disney Channel", .Slug = "disneychannel"},
                New ChannelItem() With {.Name = "Disney Junior", .Slug = "disneyjunior"},
                New ChannelItem() With {.Name = "Disney XD", .Slug = "disneyxd"},
                New ChannelItem() With {.Name = "Freeform", .Slug = "freeform"}
            }

        Public Class ChannelItem
            Public Property Name As String
            Public Property Slug As String
        End Class

#End Region

        Public Class EpisodeInformation

            Public Property EpisodeNumber As Integer?
            Public Property SeasonNumber As Integer?
            Public Property EpisodeTitle As String
            Public Property EpisodeId As Integer?

            Public Property EpisodePhotosUrl As String

        End Class

    End Class

End Namespace