Option Strict On

Imports CsQuery
Imports System.Text.RegularExpressions
Imports EpisodeImageDownloader.Infrastructure

Public Class RandomViewModel

    Private showName As String
    Private seasonNumber As Integer
    Private data As String

    Private Function ShowDownloadFolder() As String
        If Not String.IsNullOrWhiteSpace(showName) Then
            Return IO.Path.Combine(My.Settings.DownloadFolder,
                                   showName.MakeFileNameSafe)
        Else
            Return String.Empty
        End If
    End Function

    Private Function SeasonDownloadFolder() As String
        If Not String.IsNullOrWhiteSpace(showName) Then
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & seasonNumber.ToString("00"))
        Else
            Return String.Empty
        End If
    End Function

    Private Function SeasonDownloadFolder(seasonNo As Integer) As String
        If Not String.IsNullOrWhiteSpace(showName) Then
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & seasonNo.ToString("00"))
        Else
            Return String.Empty
        End If
    End Function

    Public Sub Begin()
        'Dim gsi As New GetShowInfo() With {.ShowName = "SciTech Now"}
        'If gsi.ShowDialog() = True Then
        '    showName = gsi.ShowName
        '    seasonNumber = gsi.SeasonNumber
        '    data = gsi.Data

        '    'DownloadImagesHunanTv()
        '    DownloadImagesPbsSciTechNow()

        'End If



        DownloadSequentialWithTwoNumbers(
            "http://www.historicmapworks.com/Images/Maps/US/MA/Springfield%201899/web-Plate_06,final/TileGroup0/4-{0}-{1}.jpg",
            0, 13, 0, 9,
            "C:\Users\Stephen\Downloads\_Temp\_Delete\New folder\SpringField MA 1899 Map\Plate_06")
    End Sub

#Region " Hunan TV "

    'Hunan TV
    Private Sub DownloadImagesHunanTv()
        Dim cqDoc As CQ
        Try
            cqDoc = CQ.CreateDocument(data)
        Catch ex As Exception
            MessageWindow.ShowDialog("Error parsing html: " & ex.Message, "Error parsing data")
            Exit Sub
        End Try
        Dim tvData As New TvDataSeries()

        cqDoc("li").Each(Sub(li As IDomObject)
                             Dim ep As New TvDataEpisode() With {.SeasonNumber = seasonNumber}
                             Dim episodeNameText = li.Cq.Find(".a-pic-t1").Text().Trim(CChar("第")).Trim(CChar("集"))
                             If IsNumeric(episodeNameText) Then
                                 ep.EpisodeNumber = CInt(episodeNameText)
                             End If
                             ep.EpisodeName = li.Cq.Find(".a-pic-t2").Text()
                             tvData.Episodes.Add(ep)

                             Dim re = Regex.Match(li.Cq.Find("img").Attr("src"), "(?<fullUrl>.+\.jpg)_")
                             If re.Success Then
                                 DownloadImageHunanTv(re.Groups("fullUrl").Value, ep)
                             End If

                         End Sub)

        If tvData.Episodes.Count > 0 Then
            tvData.SaveToFile(ShowDownloadFolder, showName & " " & "Season " & seasonNumber.ToString("00"))
        End If

    End Sub

    Private Sub DownloadImageHunanTv(src As String, episode As TvDataEpisode)
        If Not IO.Directory.Exists(SeasonDownloadFolder()) Then
            IO.Directory.CreateDirectory(SeasonDownloadFolder())
        End If

        ''Dim Filename = IO.Path.GetFileNameWithoutExtension(imageUrl)
        Dim extension = IO.Path.GetExtension(src)
        Dim SeasonEpisodeFilename = "S" & seasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.EpisodeName.MakeFileNameSafe() & extension
        Dim localPath = IO.Path.Combine(SeasonDownloadFolder(), SeasonEpisodeFilename.Replace(" ", "_"))
        If Not IO.File.Exists(localPath) Then
            Try
                Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                    client.DownloadFile(src, localPath)
                End Using
                'AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            Catch ex As Exception
                'AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            End Try
        Else
            'AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
        End If

    End Sub

#End Region

#Region " PbsSciTechNow "

    Private Sub DownloadImagesPbsSciTechNow()
        Dim doc = CQ.CreateDocument(data)
        Dim tvd As New TvDataSeries()
        For Each article As IDomObject In doc("article")
            Dim ep As New TvDataEpisode() With {
                .EpisodeName = article.Cq.Find("h1.entry-title").Text(),
                .Overview = article.Cq.Find("div.entry-content p").Text()
            }
            Dim episodeNoMatch = Regex.Match(ep.EpisodeName, "episode (\d+)", RegexOptions.IgnoreCase)
            If episodeNoMatch.Success Then
                Dim episodeNo As Integer = CInt(episodeNoMatch.Groups(1).Value)
                If episodeNo > 200 AndAlso episodeNo < 300 Then
                    ep.SeasonNumber = 2
                    ep.EpisodeNumber = episodeNo - 200
                Else
                    ep.SeasonNumber = 1
                    ep.EpisodeNumber = episodeNo
                End If
            End If
            ep.FirstAired = article.Cq.Find("div.premiere-date").Text().Replace("Premiered ", String.Empty).ToIso8601DateString()
            tvd.Episodes.Add(ep)
            DownloadImagePbsSciTechNow(ep, article.Cq.Find("figure img").Attr("src"))
        Next

        If tvd.Episodes.Count > 0 Then
            tvd.SaveToFile(ShowDownloadFolder, showName)
        End If
        MessageWindow.ShowDialog("Finished", "Finished")
    End Sub

    Private Sub DownloadImagePbsSciTechNow(ep As TvDataEpisode, imageUrl As String)
        Dim url = imageUrl.Replace("-480x270", String.Empty)
        Dim localFileName = "S" & ep.SeasonNumber.ToString("00") & "E" & ep.EpisodeNumber.ToString("00") & IO.Path.GetExtension(imageUrl)
        Dim localPath = IO.Path.Combine(SeasonDownloadFolder(ep.SeasonNumber), localFileName)

        If Not IO.File.Exists(localPath) Then
            If Not IO.Directory.Exists(IO.Path.GetDirectoryName(localPath)) Then
                IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(localPath))
            End If
            Try
                WebResources.DownloadFile(Url, localPath)
            Catch ex As Exception
            End Try
        End If
    End Sub


#End Region


    Private Sub DownloadSequentialWithTwoNumbers(format As String,
                                                 FirstNoStart As Integer,
                                                 FirstNoEnd As Integer,
                                                 SecondNoStart As Integer,
                                                 SecondNoEnd As Integer,
                                                 localFolder As String)
        For i = FirstNoStart To FirstNoEnd
            For j = SecondNoStart To SecondNoEnd
                Dim downloadUrl = String.Format(format, i, j)
                Dim Filename = IO.Path.GetFileName(downloadUrl)
                Dim localPath = IO.Path.Combine(localFolder, Filename)
                If Not IO.File.Exists(localPath) Then
                    If Not IO.Directory.Exists(localFolder) Then
                        IO.Directory.CreateDirectory(localFolder)
                    End If
                    WebResources.DownloadFile(downloadUrl, localPath)
                End If
            Next
        Next
    End Sub

End Class
