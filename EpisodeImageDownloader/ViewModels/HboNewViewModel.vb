Option Strict On

Imports EpisodeImageDownloader.Infrastructure
Imports CsQuery
Imports System.Runtime.Serialization

Namespace ViewModels

    Public Class HboNewViewModel
        Inherits ViewModelBase

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

        Private _showIsMiniSeries As Boolean
        Public Property ShowIsMiniSeries As Boolean
            Get
                Return _showIsMiniSeries
            End Get
            Set(value As Boolean)
                SetProperty(_showIsMiniSeries, value)
            End Set
        End Property

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName)
        End Function

        Protected Overrides Sub DownloadImages()

            'http://www.hbo.com/the-brink/episodes
            Dim indexUrl As String = String.Format("http://www.hbo.com/{0}/episodes/index.html", ShowName.ToLower().Replace(" ", "-"))
            Dim indexHtml As String
            Try
                indexHtml = WebResources.DownloadString(indexUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("While downloading index: " & Environment.NewLine & ex.Message, "Error downloading Index")
                Exit Sub
            End Try

            Dim indexDoc = CQ.CreateDocument(indexHtml)
            Dim IndexReactContentJson = indexDoc("script[data-reactid]:contains(reactContent)").Text().Replace("window.__reactContent=", String.Empty)
            Dim irc = IndexReactContentJson.FromJSON(Of IndexReactContent)()
            Dim videos = irc.Videos

            'Filter by season number if user filled out the SeasonNumber field
            If SeasonNumber.HasValue Then
                videos = (From v In videos Where v.Season = SeasonNumber.Value).ToList()
            End If

            CalculateRelativeEpisodeNumbers(videos)

            'For Each episode In videos
            '    DownloadSlideShowImages(episode)
            'Next
            Parallel.ForEach(videos, AddressOf DownloadSlideShowImages)
        End Sub

        Private Sub CalculateRelativeEpisodeNumbers(videos As List(Of HboVideo))
            Dim seasonNumbers As IEnumerable(Of Integer) = From v In videos Select v.Season Distinct
            For Each SeasonNo In seasonNumbers
                Dim episodes = From e In videos Where e.Season = SeasonNo Order By CInt(e.AbsoluteEpisodeNumber) Ascending
                Dim episodeCounter As Integer = 1
                For Each episode In episodes
                    episode.EpisodeNumber = episodeCounter
                    episodeCounter += 1
                Next
            Next
        End Sub

        Private Sub DownloadSlideShowImages(episode As HboVideo)
            Dim episodeHtml = String.Empty
            Try
                episodeHtml = WebResources.DownloadString(episode.Url)
            Catch ex As Exception
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisode(episode), .HasError = True, .NewDownload = False, .Message = "Error while downloading slideshow Html: " & ex.Message})
            End Try
            Dim episodeDoc = CQ.CreateDocument(episodeHtml)
            Dim episodeReactContentJson = episodeDoc("script[data-reactid]:contains(reactContent)").Text().Replace("window.__reactContent=", String.Empty)
            Dim erc = episodeReactContentJson.FromJSON(Of EpisodeReactContent)()
            DownloadImage(episode, erc.FullBleedImage1920Url)
            For Each imageUrl In erc.SlideshowImageUrls
                DownloadImage(episode, imageUrl)
            Next
        End Sub

        Private Sub DownloadImage(episode As HboVideo, imageUrl As String)
            Dim SeasonNo = episode.Season
            Dim episodeNumber As Integer = episode.EpisodeNumber

            Dim episodeFolder As String = "S" & SeasonNo.ToString("D2") & "E" & episodeNumber.ToString("D2") &
                "-AE" & CInt(episode.AbsoluteEpisodeNumber).ToString("D2")
            Dim episodeFolderPath As String = IO.Path.Combine(My.Settings.DownloadFolder, ShowName, "Season " & SeasonNo.ToString("D2"), episodeFolder)
            Dim localFilePath As String = IO.Path.Combine(episodeFolderPath, IO.Path.GetFileName(imageUrl))
            Dim seFilename As String = SeasonEpisode(episode) & " " & IO.Path.GetFileName(localFilePath)
            DownloadImageAddResult(imageUrl, localFilePath, seFilename)
        End Sub

        Private Function SeasonEpisode(ep As HboVideo) As String
            Return "S" & ep.Season.ToString("D2") & "E" & ep.EpisodeNumber.ToString("D2")
        End Function

#Region " Folder Functions "

        Public Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe())
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

#Region " Download Homepage Images "

        Private Const df = "C:\Users\Stephen\Downloads\_Temp\HBO"

        Public ReadOnly Property DownloadHomePageImagesCommand As ICommand
            Get
                Return New RelayCommand(Sub()
                                            Dim th As New System.Threading.Thread(AddressOf DownloadHomePageImages)
                                            th.Start()
                                        End Sub)
            End Get
        End Property

        Private Sub DownloadHomePageImages()
            NotBusy = False
            ClearEpisodeImageResults()
            Dim xml = String.Empty
            Try
                xml = WebResources.DownloadString("http://render.lv3.hbo.com/data/content/index.xml")
            Catch ex As Exception
                MessageWindow.ShowDialog(ex.Message, "Error downloading homepage xml")
                Exit Sub
            End Try

            Xml = xml.Replace("nav:", String.Empty)
            Dim doc = CQ.CreateDocument(xml)
            Dim navItems = doc("navigation group item").ToList()
            'Parallel.ForEach(navItems, AddressOf DownloadHomepageImage)
            For Each navItem In navItems
                DownloadHomepageImage(navItem)
            Next

            If IO.Directory.Exists(df) Then
                Process.Start(df)
            End If

            'doc("nav:item").Each(AddressOf DownloadHomepageImage)
            NotBusy = True
        End Sub

        Private Sub DownloadHomepageImage(item As IDomObject)
            Dim img = item.GetAttribute("img")
            img = img.Replace("-100.jpg", "-1920.jpg")

            If img IsNot Nothing Then
                If Not IO.Directory.Exists(df) Then
                    IO.Directory.CreateDirectory(df)
                End If

                Dim showName = item.Cq.Find("name").Text().Replace("[CDATA[", String.Empty).Replace("]]", String.Empty)

                Dim Filename = (showName & "_" & IO.Path.GetFileName(img)).MakeFileNameSafe()
                Dim localPath = IO.Path.Combine(df, Filename)
                If Not IO.File.Exists(localPath) Then
                    Try
                        Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                            client.DownloadFile(img, localPath)
                        End Using
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                    Catch ex As Exception
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                    End Try
                Else
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
                End If
            End If
        End Sub

#End Region

#Region " HBO Data Structures "

#Region " Series Index "

        <DataContract>
        Public Class IndexReactContent

            <DataMember(Name:="content")>
            Private Property Content As IndexContent

            Public ReadOnly Property Videos As List(Of HboVideo)
                Get
                    Return Content.Navigation.EpisodesNav.Videos
                End Get
            End Property

            Public ReadOnly Property TotalSeasons As Integer
                Get
                    Return Content.Navigation.EpisodesNav.TotalSeasons
                End Get
            End Property

        End Class

        <DataContract>
        Public Class IndexContent

            <DataMember(Name:="navigation")>
            Public Property Navigation As IndexNavigation
        End Class

        <DataContract>
        Public Class IndexNavigation

            <DataMember(Name:="episodesNav")>
            Public Property EpisodesNav As EpisodesNav
        End Class

        <DataContract>
        Public Class EpisodesNav

            <DataMember(Name:="video")>
            Public Property Videos As List(Of HboVideo)

            <DataMember(Name:="totalSeasons")>
            Public Property TotalSeasons As Integer
        End Class

        <DataContract>
        Public Class HboVideo

            <DataMember(Name:="season")>
            Public Property Season As Integer

            <DataMember(Name:="episodeNumber")>
            Public Property AbsoluteEpisodeNumber As String

            Public Property EpisodeNumber As Integer

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="description")>
            Public Property Description As String

            Private _url As String
            <DataMember(Name:="url")>
            Public Property Url As String
                Get
                    Dim videoUri As New Uri(_url, UriKind.RelativeOrAbsolute)
                    If videoUri.IsAbsoluteUri Then
                        Return videoUri.ToString
                    Else
                        Return New Uri(New Uri("http://www.hbo.com"), videoUri).ToString()
                    End If

                    'Return "http://www.hbo.com/" & _url
                End Get
                Set(value As String)
                    _url = value
                End Set
            End Property

            <DataMember(Name:="originalAirDate")>
            Public Property OriginalAirDate As String
        End Class

#End Region

#Region " Episodes  "

        <DataContract>
        Public Class EpisodeReactContent

            <DataMember(Name:="content")>
            Public Property Content As EpisodeContent

            <DataMember(Name:="imageApi")>
            Public Property ImageApi As String

            <DataMember(Name:="domain")>
            Public Property Domain As String

            Public ReadOnly Property FullBleedImage1920Url As String
                Get
                    If Content IsNot Nothing Then
                        Dim bigImage = Content.Content.Parsed.CommonFullBleedImages.Where(Function(fbi) fbi.Width = 1920).FirstOrDefault
                        If bigImage IsNot Nothing Then
                            Return bigImage.Src
                        End If
                    End If
                    Return Nothing
                End Get
            End Property

            Public ReadOnly Property SlideshowImageUrls As List(Of String)
                Get
                    Try
                        Return (From g In Content.Bundled.Galleries.First().Content.Parsed.CommonSlideshow.Group
                                Select g.Image.Where(Function(i) i.Width = 1920).FirstOrDefault.Src).ToList()
                    Catch ex As Exception
                        Return New List(Of String)
                    End Try
                End Get
            End Property

        End Class

        <DataContract>
        Public Class EpisodeContent

            <DataMember(Name:="content")>
            Public Property Content As EpisodeContentContent

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="category")>
            Public Property Category As String

            <DataMember(Name:="bundled")>
            Public Property Bundled As Bundled
        End Class

        <DataContract>
        Public Class EpisodeContentContent

            <DataMember(Name:="parsed")>
            Public Property Parsed As EpisodeContentParsed
        End Class

        <DataContract>
        Public Class EpisodeContentParsed

            <DataMember(Name:="common:FullBleedImage")>
            Public Property CommonFullBleedImages As List(Of HboImage)

        End Class

        <DataContract>
        Public Class HboImage

            <DataMember(Name:="width")>
            Public Property Width As Integer

            <DataMember(Name:="src")>
            Public Property Src As String
        End Class

        <DataContract>
        Public Class Bundled

            <DataMember(Name:="galleries")>
            Public Property Galleries As List(Of Gallery)
        End Class

        <DataContract>
        Public Class Gallery

            <DataMember(Name:="content")>
            Public Property Content As GalleriesContent

        End Class

        <DataContract>
        Public Class GalleriesContent

            <DataMember(Name:="parsed")>
            Public Property Parsed As GalleriesParsed
        End Class

        <DataContract>
        Public Class GalleriesParsed

            <DataMember(Name:="common:Slideshow")>
            Public Property CommonSlideshow As CommonSlideshow
        End Class

        <DataContract>
        Public Class CommonSlideshow

            <DataMember(Name:="group")>
            Public Property Group As List(Of SlideshowGroup)
        End Class

        <DataContract>
        Public Class SlideshowGroup

            <DataMember(Name:="thumb")>
            Public Property Thumb As String

            <DataMember(Name:="image")>
            Public Property Image As List(Of HboImage)
        End Class

#End Region

#End Region

    End Class

End Namespace
