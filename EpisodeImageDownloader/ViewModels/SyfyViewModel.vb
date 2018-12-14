Option Strict On

Imports CsQuery
Imports System.Text.RegularExpressions
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Text

Namespace ViewModels

    Public Class SyfyViewModel
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

        Private _showUrl As String
        Public Property ShowUrl As String
            Get
                Return _showUrl
            End Get
            Set(value As String)
                SetProperty(_showUrl, value)
            End Set
        End Property

        Public ReadOnly Property EpisodeDownloadFolder(SeasonNumber As Integer, EpisodeNumber As Integer) As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe,
                                       "Season " & CInt(SeasonNumber).ToString("00"),
                                       "S" & CInt(SeasonNumber).ToString("00") & "E" & CInt(EpisodeNumber).ToString("00"))
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

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(ShowUrl)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim galleryItems As List(Of GalleryItem) = GetGalleryItems()

            ''DEBUG:
            'galleryItems = New List(Of GalleryItem)
            'Dim gi As New GalleryItem()
            'gi.PhotoCount = 5
            'gi.Title = "Roots"
            'gi.SubTitle = "Season 02 Episode 0206"
            'gi.Description = ""
            'gi.Type = "Episodic Gallery"
            'gi.CoverImage = "http://www.syfy.com/_cache/images/assets/haven/2011-08/s02_e0206_02_131255990624___CC___225x128.jpg"
            'gi.Path = "/haven/photos/season_02_episode_0206"

            'galleryItems.Add(gi)
            ''END DEBUG

            Parallel.ForEach(galleryItems, AddressOf DownloadEpisodeImages)
            ''Parallel.ForEach(galleryItems.Where(Function(g) g.Type = "Episodic Gallery"), AddressOf DownloadEpisodeImages)


            'For Each gi In galleryItems
            '    DownloadEpisodeImages(gi)
            'Next

        End Sub

        Private Function GetGalleryItems() As List(Of GalleryItem)
            Dim returnList As New List(Of GalleryItem)
            Dim html As String
            Using client As New Infrastructure.ChromeWebClient()
                client.AllowAutoRedirect = True
                html = client.DownloadString(ShowUrl)
            End Using
            Dim itemsmatch As Match = Regex.Match(html, ",\s?""items"":(.+])")
            If itemsmatch.Success Then
                Dim ser As New DataContractJsonSerializer(GetType(List(Of GalleryItem)))
                Dim ms As New IO.MemoryStream(UTF8Encoding.UTF8.GetBytes(itemsmatch.Groups(1).Value))
                returnList = CType(ser.ReadObject(ms), List(Of GalleryItem))
            End If

            Dim cqDoc As CQ = CQ.Create(html)
            cqDoc("#photo-list li").Each(Sub(li As idomobject)
                                             Dim gi As New GalleryItem() With {
                                                 .Path = li.Cq().Find("a").Attr("href"),
                                                 .SubTitle = li.Cq().Find("div.subTitle").Text().Trim()
                                             }
                                             returnList.Add(gi)
                                         End Sub)
            Return returnList
        End Function

        Private Sub DownloadEpisodeImages(galleryItem As GalleryItem)
            If galleryItem.SeasonNumber <> 0 AndAlso galleryItem.EpisodeNumber <> 0 Then
                If Not IO.Directory.Exists(EpisodeDownloadFolder(galleryItem.SeasonNumber, galleryItem.EpisodeNumber)) Then
                    IO.Directory.CreateDirectory(EpisodeDownloadFolder(galleryItem.SeasonNumber, galleryItem.EpisodeNumber))
                End If

                Dim galleryImages As List(Of GalleryImage) = GetGalleryImages(galleryItem.Uri)

                ''DEBUG:
                'galleryImages = New List(Of GalleryImage)
                'galleryImages.Add(New GalleryImage() With {.FullSize = New GalleryImage.ImageInfo() With {.Url = "http://www.syfy.com/_cache/assets/assets/haven/2011-08/s02_e0206_01_131255992035.jpg"}})
                ''END DEBUG

                Parallel.ForEach(galleryImages, Sub(galleryImage)
                                                    DownloadImage(galleryImage, galleryItem)
                                                End Sub)

                'For Each gi In galleryImages
                '    DownloadImage(gi, galleryItem)
                'Next

            End If
        End Sub

        Private Function GetGalleryImages(galleryUri As Uri) As List(Of GalleryImage)
            Dim returnList As New List(Of GalleryImage)
            Dim html As String
            Using client As New Infrastructure.ChromeWebClient()
                html = client.DownloadString(galleryUri)
            End Using
            Dim imagesMatch As Match = Regex.Match(html, "images:\s?(.+])")
            If imagesMatch.Success Then
                Dim ser As New DataContractJsonSerializer(GetType(List(Of GalleryImage)))
                Dim ms As New IO.MemoryStream(UTF8Encoding.UTF8.GetBytes(imagesMatch.Groups(1).Value))
                returnList = CType(ser.ReadObject(ms), List(Of GalleryImage))
            End If
            Return returnList
        End Function

        Private Sub DownloadImage(galImage As GalleryImage, galItem As GalleryItem)

            Dim imageUrl = galImage.FullSize.Url

            Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
            Dim SeasonEpisodeFilename = "S" & galItem.SeasonNumber.ToString("00") & "E" & galItem.EpisodeNumber.ToString("00") & " - " & Filename
            Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(galItem.SeasonNumber, galItem.EpisodeNumber), SeasonEpisodeFilename)
            MyBase.DownloadImageAddResult(imageUrl, localPath)
            'If Not IO.File.Exists(localPath) Then
            '    Try
            '        Using client As New Infrastructure.ChromeWebClient
            '            client.DownloadFile(imageUrl, localPath)
            '        End Using
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If

        End Sub

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

        <DataContract>
        Public Class GalleryItem

            '<DataMember(Name:="photoCount")>
            'Public Property PhotoCount As Integer

            '<DataMember(Name:="title")>
            'Public Property Title As String

            <DataMember(Name:="subTitle")>
            Public Property SubTitle As String

            '<DataMember(Name:="description")>
            'Public Property Description As String

            '<DataMember(Name:="type")>
            'Public Property Type As String

            '<DataMember(Name:="coverImage")>
            'Public Property CoverImage As String

            <DataMember(Name:="path")>
            Public Property Path As String

            'Public Sub New()
            '    seasonEpisodeRegex = New Regex("season (?<seasonNumber>\d+)[\s-]+?Episode (?<episodeNumber>\d+)", RegexOptions.IgnoreCase)
            'End Sub

            'Private seasonEpisodeRegex As Regex

            Public ReadOnly Property SeasonNumber As Integer
                Get
                    Dim match = Regex.Match(SubTitle, "season (?<seasonNumber>\d+)[\s-]+?Episode (?<episodeNumber>\d+)", RegexOptions.IgnoreCase)
                    'seasonEpisodeRegex.Match(SubTitle)
                    If match.Success Then
                        Return CInt(match.Groups("seasonNumber").Value)
                    Else
                        Return 0
                    End If
                End Get
            End Property

            Public ReadOnly Property EpisodeNumber As Integer
                Get
                    Dim match = Regex.Match(SubTitle, "season (?<seasonNumber>\d+)[\s-]+?Episode (?<episodeNumber>\d+)", RegexOptions.IgnoreCase)
                    'seasonEpisodeRegex.Match(SubTitle)
                    If match.Success Then
                        Return CInt(match.Groups("episodeNumber").Value)
                    Else
                        Return 0
                    End If
                End Get
            End Property

            Public ReadOnly Property Uri As Uri
                Get
                    Dim returnUri As New Uri(Path, UriKind.RelativeOrAbsolute)
                    If Not returnUri.IsAbsoluteUri Then
                        returnUri = New Uri(New Uri("http://www.syfy.com"), returnUri)
                    End If
                    Return returnUri
                End Get
            End Property

        End Class

        <DataContract>
        Public Class GalleryImage

            <DataContract>
            Public Class ImageInfo

                <DataMember(Name:="url")>
                Public Property Url As String

            End Class

            <DataMember(Name:="fullsize")>
            Public Property FullSize As ImageInfo

        End Class

    End Class

End Namespace
