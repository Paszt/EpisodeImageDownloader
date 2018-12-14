Imports System.Text.RegularExpressions
Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization

Namespace ViewModels

    Public Class NbcViewModel
        Inherits ViewModels.ViewModelBase

        'Private _client As Infrastructure.ChromeWebClient

        Public Sub New()
            EpisodeInfos = New ObservableCollection(Of EpisodeInfo)
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

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _episodeInfos As ObservableCollection(Of EpisodeInfo)
        Public Property EpisodeInfos As ObservableCollection(Of EpisodeInfo)
            Get
                Return _episodeInfos
            End Get
            Set(value As ObservableCollection(Of EpisodeInfo))
                SetProperty(_episodeInfos, value)
            End Set
        End Property

        Public ReadOnly Property EpisodeDownloadFolder(EpisodeNumber As Integer) As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe,
                                       "Season " & CInt(SeasonNumber).ToString("00"),
                                       "S" & CInt(SeasonNumber).ToString("00") & "E" & CInt(EpisodeNumber).ToString("00"))
            End Get
        End Property

        Public ReadOnly Property SeasonDownloadFolder As String
            Get
                If SeasonNumber.HasValue AndAlso Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe,
                                           "Season " & CInt(SeasonNumber).ToString("00"))
                Else
                    Return String.Empty
                End If
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return EpisodeInfos.Count > 0 AndAlso
                   EpisodeInfoValid() AndAlso
                   Not String.IsNullOrEmpty(ShowName) AndAlso
                   SeasonNumber.HasValue
        End Function

        Private Function EpisodeInfoValid() As Boolean
            For Each info In EpisodeInfos
                If info.EpisodeNumber.HasValue AndAlso Not String.IsNullOrEmpty(info.EpisodeUrl) Then
                    Return True
                End If
            Next
            Return False
        End Function

        'Protected Overrides Sub DownloadImages()
        '    NotBusy = False
        '    ClearEpisodeImageResults()
        '    Using _client As New Infrastructure.ChromeWebClient()
        '        For Each info In EpisodeInfos
        '            If info.EpisodeNumber.HasValue Then
        '                'html = _client.DownloadString(info.EpisodeUrl)
        '                'Dim dom = CsQuery.CQ.CreateDocument(html)
        '                'dom("ul.gallery-nav-pages img").Each(Sub(img As CsQuery.IDomObject)
        '                '                                         DownloadImage(img, info.EpisodeNumber)
        '                '                                     End Sub)
        '                Dim npp As New NbcPhotosPage()
        '                'Dim pageNumber As Integer = 1
        '                Dim jsonUrl = Regex.Replace(info.EpisodeUrl, "\/$", String.Empty) & "/gallery-feed"
        '                Do
        '                    Dim html = String.Empty
        '                    Try
        '                        html = _client.DownloadString(jsonUrl)
        '                    Catch ex As Exception
        '                        MessageWindow.ShowDialog(ex.Message, "Error downloading json")
        '                    End Try
        '                    npp = html.FromJSON(Of NbcPhotosPage)()
        '                    If npp IsNot Nothing Then
        '                        Parallel.ForEach(npp.Photos, Sub(img As Photo)
        '                                                         DownloadImage(img, info.EpisodeNumber)
        '                                                     End Sub)
        '                        'For Each img In npp.Photos
        '                        '    DownloadImage(img, info.EpisodeNumber)
        '                        'Next
        '                        jsonUrl = npp.Gallery.NextJsonUrl
        '                    Else
        '                        MessageWindow.ShowDialog("No Images found for URL: " & Environment.NewLine & jsonUrl, "No Images!")
        '                    End If
        '                Loop Until npp Is Nothing OrElse npp.Gallery.NextJsonUrl Is Nothing
        '            End If
        '        Next
        '    End Using
        '    NotBusy = True
        'End Sub

        Protected Overrides Sub DownloadImages()
            Using _client As New Infrastructure.ChromeWebClient()
                For Each info In EpisodeInfos
                    If info.EpisodeNumber.HasValue Then
                        Dim html = _client.DownloadString(info.EpisodeUrl)
                        Dim dom = CsQuery.CQ.CreateDocument(html)
                        'dom(".slick-track .slick-slide img").Each(Sub(img As CsQuery.IDomObject)
                        '                                              DownloadImage(img, info.EpisodeNumber.Value)
                        '                                          End Sub)
                        Parallel.ForEach(dom(".slick-track .slick-slide img"), Sub(img As CsQuery.IDomObject)
                                                                                   DownloadImage(img, info.EpisodeNumber.Value)
                                                                               End Sub)
                    End If
                Next
            End Using
        End Sub

        'Private Sub DownloadImage(img As CsQuery.IDomObject, EpisodeNumber As Integer)
        '    If Not IO.Directory.Exists(EpisodeDownloadFolder(EpisodeNumber)) Then
        '        IO.Directory.CreateDirectory(EpisodeDownloadFolder(EpisodeNumber))
        '    End If
        '    Dim imageUrl = img.Cq.Attr("src")
        '    imageUrl = imageUrl.Replace(New Regex("\/styles\/.+\/public").Match(imageUrl).Value, String.Empty)
        '    Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
        '    Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(EpisodeNumber), Filename)
        '    If Not IO.File.Exists(localPath) Then
        '        Try
        '            _client.DownloadFile(imageUrl, localPath)
        '            AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
        '        Catch ex As Exception
        '            AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
        '        End Try
        '    Else
        '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
        '    End If
        'End Sub

        'Private Sub DownloadImage(nbcPhoto As Photo, EpisodeNumber As Integer)
        '    Dim imageUrl = Regex.Replace(nbcPhoto.Sizes.Full, "\/styles\/.+\/public", String.Empty)
        '    Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
        '    Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(EpisodeNumber), Filename)
        '    MyBase.DownloadImageAddResult(imageUrl, localPath)
        'End Sub

        Private Sub DownloadImage(img As CsQuery.IDomObject, EpisodeNo As Integer)
            Dim imageUrl = Regex.Replace(img.Cq.Attr("src"), "\/styles\/.+\/public", String.Empty)
            Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
            Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(EpisodeNo), Filename)
            MyBase.DownloadImageAddResult(imageUrl, localPath)
        End Sub

        Public Function SeasonDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(SeasonDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(SeasonDownloadFolder)
                                                       End Sub, AddressOf SeasonDownloadFolderExists)
            End Get
        End Property

#Region " NBC JSON Data Structures "

        <DataContract>
        Public Class Sizes

            <DataMember(Name:="thumb")>
            Public Property Thumb As String

            <DataMember(Name:="slider")>
            Public Property Slider As String

            <DataMember(Name:="full")>
            Public Property Full As String
        End Class

        <DataContract>
        Public Class Photo

            <DataMember(Name:="id")>
            Public Property Id As String

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="description")>
            Public Property Description As String

            <DataMember(Name:="sizes")>
            Public Property Sizes As Sizes
        End Class

        <DataContract>
        Public Class Gallery

            <DataMember(Name:="id")>
            Public Property Id As String

            <DataMember(Name:="baseURL")>
            Public Property BaseURL As String

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="page")>
            Public Property Page As Integer

            <DataMember(Name:="total")>
            Public Property Total As String

            <DataMember(Name:="pageSize")>
            Public Property PageSize As String

            <DataMember(Name:="slidesPerInterstitial")>
            Public Property SlidesPerInterstitial As Integer

            <DataMember(Name:="base")>
            Public Property BaseJsonUrl As String

            <DataMember(Name:="prev")>
            Public Property PrevJsonUrl As String

            <DataMember(Name:="next")>
            Public Property NextJsonUrl As String

        End Class

        <DataContract>
        Public Class NbcPhotosPage

            <DataMember(Name:="photos")>
            Public Property Photos As List(Of Photo)

            <DataMember(Name:="gallery")>
            Public Property Gallery As Gallery

        End Class


#End Region


    End Class

    Public Class EpisodeInfo

        Public Property EpisodeNumber As Integer?
        Public Property EpisodeUrl As String

    End Class



End Namespace