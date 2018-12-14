Option Strict On

Imports CsQuery
Imports System.Text.RegularExpressions

Namespace ViewModels

    Public Class CrunchyrollHtmlViewModel
        Inherits ViewModels.ViewModelBase

        Private _client As Infrastructure.ChromeWebClient

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

        Private _imageSize As String = "full"
        Public Property ImageSize As String
            Get
                Return _imageSize
            End Get
            Set(value As String)
                SetProperty(_imageSize, value)
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

            Dim fragment As CQ
            Try
                fragment = CQ.CreateFragment(HtmlText)
            Catch ex As Exception
                MessageWindow.ShowDialog(ex.Message, "Error parsing HTML")
                Exit Sub
            End Try

            _client = New Infrastructure.ChromeWebClient()
            Dim tvData As New TvDataSeries()

            For Each li In fragment("li[id^=""showview""]")
                Dim episodeMatchResult As Match = Regex.Match(li.Cq.Find("span.series-title").Text().Trim().ToLower(), "episode (\d+)")
                If episodeMatchResult.Success Then
                    Dim crunchyEpisode As New TvDataEpisode() With {.EpisodeNumber = CInt(episodeMatchResult.Groups(1).Value)}
                    tvData.Episodes.Add(crunchyEpisode)
                    Dim imageUri = New Uri(li.Cq.Find("img").FirstOrDefault().Cq.Attr("src").
                                           Replace("_wide.jpg", "_" & ImageSize & ".jpg").
                                           Replace("_widestar.jpg", "_" & ImageSize & ".jpg"))
                    Dim episodeUri As Uri = Nothing
                    If Uri.TryCreate(li.Cq.Find("a").FirstOrDefault().Cq.Attr("href"), UriKind.RelativeOrAbsolute, episodeUri) Then
                        If Not episodeUri.IsAbsoluteUri Then
                            episodeUri = New Uri(New Uri("http://www.crunchyroll.com/"), episodeUri.ToString())
                        End If
                        GetEpisodeInfo(episodeUri, crunchyEpisode)
                    End If
                    If crunchyEpisode.EpisodeName IsNot Nothing Then
                        DownloadImage(crunchyEpisode, imageUri)
                    End If
                End If
            Next

            _client.Dispose()
            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(DownloadFolder, ShowName & " " & "Season " & SeasonNumber.Value.ToString("00"))
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If
        End Sub

        Private Sub GetEpisodeInfo(EpisodeUri As Uri, ByRef crunchyEpisode As TvDataEpisode)
            Dim html As String
            Try
                html = _client.DownloadString(EpisodeUri)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading episode info: " & ex.Message & Environment.NewLine & Environment.NewLine & EpisodeUri.ToString, "Episode #" & crunchyEpisode.EpisodeNumber)
                Exit Sub
            End Try

            Dim doc = CQ.CreateDocument(html)
            'crunchyEpisode.EpisodeName = doc("#showmedia_about_info h4").FirstOrDefault().Cq.Text().Trim(Chr(147), Chr(148), CChar(" "))
            Dim titleH4 = doc("#showmedia_about_info h4").FirstOrDefault()
            If titleH4 IsNot Nothing Then
                ' Chr(147) = “    Chr(148) = ”
                crunchyEpisode.EpisodeName = titleH4.Cq.Text().Trim(Chr(147), Chr(148), CChar(" "))
            Else
                crunchyEpisode.EpisodeName = "Episode " & crunchyEpisode.EpisodeNumber
            End If
            crunchyEpisode.Overview = doc("#showmedia_about_info p.description").Contents().Filter(Function(a) a.NodeType = NodeType.TEXT_NODE).Text().Trim()
            crunchyEpisode.Overview &= " " & doc("#showmedia_about_info p.description span.more").Text().Trim()
            crunchyEpisode.Overview = crunchyEpisode.Overview.Trim().Replace(Environment.NewLine, String.Empty).Replace(Chr(10), String.Empty)
            crunchyEpisode.FirstAired = doc("#showmedia_about_info_details div:nth-of-type(3) span").Text().ToIso8601DateString()
            'Dim premiumDateString = doc("#showmedia_about_info_details div:nth-of-type(3) span").Text()
            'Dim dte As Date
            'If Date.TryParse(premiumDateString, dte) Then
            '    crunchyEpisode.FirstAired = dte.ToString("yyyy-MM-dd")
            'Else
            '    crunchyEpisode.FirstAired = premiumDateString
            'End If
            crunchyEpisode.SeasonNumber = SeasonNumber.Value
        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode, imageUri As Uri)
            Dim filename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("000") & "_" & episode.EpisodeName.MakeFileNameSafe().Replace(" ", "-") & ".jpg"
            Dim localPath = IO.Path.Combine(DownloadFolder, "Season " & episode.SeasonNumber.ToString("00"), filename)
            MyBase.DownloadImageAddResult(imageUri.ToString(), localPath)

            'If Not IO.File.Exists(localPath) Then
            '    If Not IO.Directory.Exists(IO.Path.GetDirectoryName(localPath)) Then
            '        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(localPath))
            '    End If
            '    Try
            '        _client.DownloadFile(imageUri, localPath)
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If
        End Sub

        Public Shared ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"full", "fwide"})
            End Get
        End Property

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
