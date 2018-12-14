Imports EpisodeImageDownloader.Infrastructure
Imports System.Text.RegularExpressions
Imports CsQuery

Namespace ViewModels

    Public Class HboViewModel
        Inherits ViewModelBase

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

        Private Sub Test()
            ClearEpisodeImageResults()
            NotBusy = False
            System.Threading.Thread.Sleep(2000)
            AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = "S01E01", .HasError = False, .Message = "Downloaded", .NewDownload = True})
            System.Threading.Thread.Sleep(2000)
            AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = "S01E02", .HasError = True, .Message = "Error downloading image", .NewDownload = False})
            System.Threading.Thread.Sleep(2000)
            AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = "S01E03", .HasError = False, .Message = "Already Downloaded", .NewDownload = False})
            NotBusy = True
        End Sub

        Protected Overrides Sub DownloadImages()
            ' http://www.hbo.com/data/content/girls/episodes/index.xml
            Dim episodesOrParts As String = "episodes"
            If ShowIsMiniSeries = True Then
                episodesOrParts = "parts"
            End If
            Dim indexUrl As String = String.Format("http://www.hbo.com/data/content/{0}/{1}/index.xml?limit=none&order=date-asc", ShowName.ToLower().Replace(" ", "-"), episodesOrParts)
            Dim downloadedXML As String = String.Empty
            _client = New ChromeWebClient
            Try
                downloadedXML = _client.DownloadString(indexUrl)
            Catch ex As Exception
                ''MessageBox.Show("While downloading index: " & Environment.NewLine & ex.Message)
                MessageWindow.ShowDialog("While downloading index: " & Environment.NewLine & ex.Message, "Error downloading Index")
                Exit Sub
            End Try

            Try
                Dim indexDoc As XDocument = XDocument.Parse(FixBadXml(downloadedXML))
                Dim previousSeason As String = String.Empty
                Dim seasonEpisodeNumber As Integer = 0
                For Each vid In indexDoc.Descendants("video")
                    Dim season As String = CInt(vid.Element("season").Value).ToString("00")
                    If season <> previousSeason Then
                        previousSeason = season
                        seasonEpisodeNumber = 0
                    End If
                    Dim episodeNumber As String = CInt(vid.Element("episodeNumber").Value).ToString("00")
                    If episodeNumber <> "00" Then
                        seasonEpisodeNumber += 1
                    End If
                    If (SeasonNumber.HasValue AndAlso CInt(season) = SeasonNumber.Value) OrElse (Not SeasonNumber.HasValue) Then
                        Dim imageUrl As String = vid.Element("thumbnailImage").Value.Replace("200.jpg", "1920.jpg")
                        Dim episodeXmlSlideshowUrl As String = "http://www.hbo.com/data/content/" & vid.Element("url").Value.Replace("index.html", "slideshow.xml")
                        DownloadEpisodeImages(season, episodeNumber, seasonEpisodeNumber, imageUrl, episodeXmlSlideshowUrl)
                    End If
                Next
            Catch ex As Exception
                MessageWindow.ShowDialog("Error", ex.Message)
            End Try
            _client.Dispose()
        End Sub

        Private Function FixBadXml(originalXml As String) As String
            Dim regex = New Regex("&(?!quot;|apos;|amp;|lt;|gt;#x?.*?;)")
            Dim replaced = From line In originalXml.Split(Chr(10))
                           Select IIf(Not line.Contains("CDATA"), regex.Replace(line, "&amp;"), line)
            Return String.Join(Chr(10), replaced)
        End Function

        Private Sub DownloadEpisodeImages(SeasonNumber As String,
                                          episodeNumber As String,
                                          seasonEpisodeNumber As Integer,
                                          imageUrl As String,
                                          XmlSlideshowUrl As String)
            Dim episodeFolder As String = "S" & SeasonNumber & "E" & seasonEpisodeNumber.ToString("00") & " (Ep" & episodeNumber & ")"
            Dim episodeFolderPath As String = IO.Path.Combine(My.Settings.DownloadFolder, ShowName, "Season " & SeasonNumber, episodeFolder)
            'If Not IO.Directory.Exists(episodeFolderPath) Then
            '    IO.Directory.CreateDirectory(episodeFolderPath)
            'End If
            Dim localFilePath As String = IO.Path.Combine(episodeFolderPath, IO.Path.GetFileName(imageUrl))
            DownloadImage(SeasonNumber, seasonEpisodeNumber.ToString("00"), imageUrl, localFilePath)

            Dim html = _client.DownloadString(XmlSlideshowUrl)
            Dim slideShowDoc As XDocument = XDocument.Parse(html, LoadOptions.None)
            'Dim slideShowDoc As XDocument = XDocument.Load(XmlSlideshowUrl, LoadOptions.None)
            For Each imagePath In slideShowDoc.Descendants("imagePath").Where(Function(ip) ip.Attribute("width").Value = "1920")
                Dim url As String = imagePath.Element("path").Value
                localFilePath = IO.Path.Combine(episodeFolderPath, IO.Path.GetFileName(url))
                DownloadImage(SeasonNumber, seasonEpisodeNumber.ToString("00"), url, localFilePath)
            Next
        End Sub

        Private Sub DownloadImage(SeasonNumber As String, episodeNumber As String, imageUrl As String, localFilePath As String)
            Dim seFilename As String = "S" & SeasonNumber & "E" & episodeNumber & " " & IO.Path.GetFileName(localFilePath)
            MyBase.DownloadImageAddResult(imageUrl, localFilePath, seFilename)
            'If Not IO.File.Exists(localFilePath) Then
            '    Try
            '        _client.DownloadFile(imageUrl, localFilePath)
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = seFilename, .Message = "Downloaded", .NewDownload = True, .HasError = False})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = seFilename, .Message = "Error:" & ex.Message, .NewDownload = False, .HasError = True})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = seFilename, .Message = "Already Downloaded", .NewDownload = False, .HasError = False})
            'End If
        End Sub


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

            Dim xml = WebResources.DownloadString("http://render.lv3.hbo.com/data/content/index.xml")
            xml = xml.Replace("nav:", String.Empty)
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

    End Class

End Namespace
