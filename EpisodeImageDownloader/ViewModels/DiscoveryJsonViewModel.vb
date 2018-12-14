Option Strict On

Namespace ViewModels

    Public Class DiscoveryJsonViewModel
        Inherits ViewModels.ViewModelBase

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

        Private _htmlText As String
        Public Property HtmlText As String
            Get
                Return _htmlText
            End Get
            Set(value As String)
                SetProperty(_htmlText, value)
            End Set
        End Property

        Private _isHtmlInputVisible As Boolean
        Public Property IsHtmlInputVisible As Boolean
            Get
                Return _isHtmlInputVisible
            End Get
            Set(value As Boolean)
                SetProperty(_isHtmlInputVisible, value)
                OnPropertyChanged("IsHtmlInputNotVisible")
            End Set
        End Property

        Public ReadOnly Property IsHtmlInputNotVisible As Boolean
            Get
                Return Not _isHtmlInputVisible
            End Get
        End Property

        Public ReadOnly Property DownloadFolder() As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe())
            End Get
        End Property

#End Region

        Public Sub New()
            IsHtmlInputVisible = True
            ShowName = String.Empty
        End Sub

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(HtmlText) AndAlso
                   SeasonNumber.HasValue
        End Function

        Protected Overrides Sub DownloadImages()
            IsHtmlInputVisible = False

            Dim tvData As New TvDataSeries()

            Dim eps = HtmlText.FromJSONArray(Of DiscoveryEpisode)()
            For Each ep In eps
                Dim tvDataEp As New TvDataEpisode With {
                    .EpisodeName = ep.Name,
                    .EpisodeNumber = ep.EpisodeNumber,
                    .FirstAired = ep.FirstAired,
                    .Overview = ep.Description.Standard,
                    .SeasonNumber = ep.Season.Number
                }
                tvData.Episodes.Add(tvDataEp)
                Dim Image16x9 = ep.Image.Links.Where(Function(l) l.Rel = "16x9").First()
                If Image16x9 IsNot Nothing Then
                    DownloadImage(tvDataEp, Image16x9.Href.Replace("{width}", "1920"))
                End If
            Next

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(DownloadFolder, ShowName & " " & "Season " & SeasonNumber.Value.ToString("00"))
            Else
                MessageWindow.ShowDialog("No episodes were found in JSON", "No Episodes Found")
            End If
        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode, imageUrl As String)
            Dim filename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("000") & "_" & episode.EpisodeName.MakeFileNameSafe().Replace(" ", "-") & ".jpg"
            Dim localPath = IO.Path.Combine(DownloadFolder, "Season " & episode.SeasonNumber.ToString("00"), filename)
            MyBase.DownloadImageAddResult(imageUrl, localPath)
        End Sub

#Region " Commands "

        Public Function DownloadFolderExists() As Boolean
            Return IO.Directory.Exists(DownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        If IO.Directory.Exists(DownloadFolder) Then
                            Process.Start(DownloadFolder)
                        End If
                    End Sub, AddressOf DownloadFolderExists)
            End Get
        End Property

        Public ReadOnly Property ShowHtmlInputCommand() As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        IsHtmlInputVisible = True
                    End Sub)
            End Get
        End Property

#End Region

    End Class

End Namespace
