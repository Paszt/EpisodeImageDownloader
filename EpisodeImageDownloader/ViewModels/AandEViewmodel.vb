Imports System.Collections.ObjectModel
Imports CsQuery

Namespace ViewModels

    Public Class AandEViewmodel
        Inherits ViewModelBase

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

        Protected Overrides Sub DownloadImages()
            Parallel.ForEach(EpisodeInfos, AddressOf DownloadEpisodeImages)

            'For Each info In EpisodeInfos
            '    DownloadEpisodeImages(info)
            'Next info
        End Sub

        Private Sub DownloadEpisodeImages(info As EpisodeInfo)
            If info.EpisodeNumber.HasValue Then
                Dim html = Infrastructure.WebResources.DownloadString(info.EpisodeUrl)
                Dim doc = CQ.Create(html)
                Dim images = doc("#picture_selector li img").ToList()
                Parallel.ForEach(images, Sub(img As IDomObject)
                                             DownloadImage(img, info.EpisodeNumber.Value)
                                         End Sub)
                'For Each img In images
                '    DownloadImage(img, info.EpisodeNumber.Value)
                'Next
            End If
        End Sub

        Private Sub DownloadImage(img As IDomObject, EpisodeNumber As Integer)
            Dim imageUrl = img.Cq.Attr("src").Replace("/picture_thumbnail", String.Empty)
            Dim Filename = "S" & SeasonNumber.Value.ToString("00") & "E" & EpisodeNumber.ToString("00") & "_" &
                                 IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
            Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(EpisodeNumber), Filename)
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

    End Class

End Namespace