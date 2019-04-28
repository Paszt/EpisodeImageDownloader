Imports System.Text.RegularExpressions
Imports CsQuery

Namespace ViewModels

    Public Class RokuViewModel
        Inherits ViewModelBase

        Private tvData As TvDataSeries

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

        Private _inputHtml As String
        ''' <summary> Html from any episode video page </summary>
        Public Property InputHtml As String
            Get
                Return _inputHtml
            End Get
            Set(value As String)
                SetProperty(_inputHtml, value)
            End Set
        End Property

        Private _isHtmlInputVisible As Boolean = True
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
                If ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) Then
                    Return True
                End If
                Return Not _isHtmlInputVisible
            End Get
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                Return IO.Path.Combine(ShowDownloadFolder, "Season " & CInt(SeasonNumber).ToString("00"))
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
                   Not String.IsNullOrWhiteSpace(InputHtml)
        End Function

        Protected Overrides Sub DownloadImages()
            IsHtmlInputVisible = False
            tvData = New TvDataSeries()

            'TODO Finish Roku DownloadImages
            Dim doc = CQ.CreateDocument(InputHtml)
            For Each ep In doc.Find("div.roku-season-episode")
                Dim cqEp = ep.Cq
                Dim tvEp As New TvDataEpisode() With {
                    .Overview = cqEp.Find(".roku-episode-description").Text().Trim(),
                    .EpisodeName = cqEp.Find(".roku-episode-title").Attr("title").Trim(),
                    .ImageUrl = cqEp.Find(".roku-episode-poster img").Attr("src")
                }

                tvEp.ImageUrl = Regex.Replace(tvEp.ImageUrl, "\/presets\/\w+", String.Empty)

                Dim metaMatch = Regex.Match(cqEp.Find(".roku-episode-meta").Text(), "S(?<season>\d+):E(?<episode>\d+)\s+\|\s+(?<date>[A-Za-z\s\d,]+)\s\|")
                If metaMatch.Success Then
                    tvEp.SeasonNumber = CInt(metaMatch.Groups("season").Value)
                    tvEp.EpisodeNumber = CInt(metaMatch.Groups("episode").Value)
                    Dim dte As Date
                    If Date.TryParse(metaMatch.Groups("date").Value, dte) Then
                        tvEp.FirstAired = dte.ToIso8601DateString
                    End If
                    tvData.Episodes.Add(tvEp)
                    DownloadImage(tvEp)
                End If
            Next

            If tvData.Episodes.Count > 0 Then
                ' save each season in a separate file
                For Each seasonNo In tvData.SeasonNumbersDistinct
                    Dim seasonTvdata As New TvDataSeries With {
                        .Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
                                                            OrderBy(Function(t) t.SeasonNumber).
                                                            ThenBy(Function(t) t.EpisodeNumber).ToList()
                    }
                    seasonTvdata.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & seasonNo)
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If

        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            If String.IsNullOrEmpty(extension) Then
                extension = ".jpg"
            End If
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" &
                episode.EpisodeName.MakeFileNameSafeNoSpaces() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename)
            DownloadImageAddResult(episode.ImageUrl, localPath)
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

        Public ReadOnly Property ShowHtmlInputCommand() As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           IsHtmlInputVisible = True
                                                       End Sub)
            End Get
        End Property

    End Class

End Namespace

