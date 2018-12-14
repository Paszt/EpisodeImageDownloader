Option Strict On

Imports System.Web.Script.Serialization
Imports System.IO
Imports System.Text
Imports System.Runtime.Serialization.Json

Namespace ViewModels

    Public Class CrunchyrollViewModel
        Inherits ViewModels.ViewModelBase

        Private _client As Infrastructure.ChromeWebClient

#Region " Properties "

        Private _showName As String = "Ace of the Diamond"
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _seasonNumber As Integer? = 1
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _collectionId As String = "21301"
        Public Property CollectionId As String
            Get
                Return _collectionId
            End Get
            Set(value As String)
                SetProperty(_collectionId, value)
            End Set
        End Property

        Private _ImagesDownloaded As Boolean = False
        Public Property ImagesDownloaded As Boolean
            Get
                Return _ImagesDownloaded
            End Get
            Set(value As Boolean)
                SetProperty(_ImagesDownloaded, value)
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

        Public ReadOnly Property DownloadFolder() As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe(),
                                       "Season " & CInt(SeasonNumber).ToString("00"))
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   SeasonNumber.HasValue AndAlso
                   Not String.IsNullOrWhiteSpace(CollectionId)
        End Function

        Protected Overrides Sub DownloadImages()
            ImagesDownloaded = False
            _client = New Infrastructure.ChromeWebClient()
            Dim urlFormat = "http://www.crunchyroll.com/ajax/?req=RpcApiMedia_GetCollectionCarouselPage&group_id=&collection_id={0}&first_index={1}"
            Dim responseDictionary As New Dictionary(Of String, Object)
            Dim serializer As New JavaScriptSerializer()
            Dim i As Integer = 0
            Dim tvData As New TvDataSeries()
            Do
                Dim url = String.Format(urlFormat, CollectionId, i)
                Dim jsonResponse = _client.DownloadString(url)
                jsonResponse = jsonResponse.Substring(jsonResponse.IndexOf("{"), jsonResponse.LastIndexOf("}") - jsonResponse.IndexOf("{") + 1)
                responseDictionary = CType(serializer.Deserialize(Of Object)(jsonResponse), Dictionary(Of String, Object))
                Dim data As New Dictionary(Of String, Object)
                If responseDictionary.ContainsKey("data") AndAlso
                   responseDictionary("data") IsNot Nothing AndAlso
                   responseDictionary("data").GetType Is GetType(Dictionary(Of String, Object)) Then
                    data = CType(responseDictionary("data"), Global.System.Collections.Generic.Dictionary(Of String, Object))
                    For j As Integer = 0 To data.Count - 1
                        Dim crEpisode = DownloadImage(CStr(data.ElementAt(j).Value))
                        If crEpisode IsNot Nothing Then
                            tvData.Episodes.Add(New TvDataEpisode() With {.EpisodeName = crEpisode.Name,
                                                                          .EpisodeNumber = crEpisode.EpisodeNumber,
                                                                          .Overview = crEpisode.Overview,
                                                                          .SeasonNumber = crEpisode.SeasonNumber})
                        End If
                    Next
                Else
                    Exit Do
                End If

                i += 5
            Loop

            ' Save TvXml
            tvData.SaveToFile(DownloadFolder, ShowName)

            ' Finish
            ImagesDownloaded = True
            _client.Dispose()
        End Sub

        Private Function DownloadImage(mediaHtml As String) As CrunchyrollEpisode
            Dim dom = CsQuery.CQ.CreateDocument(mediaHtml)
            Dim imgSrc = dom("img").Attr("src").Replace("_wide.jpg", "_" & ImageSize & ".jpg")
            Dim episodeNumber As Integer
            If Not Integer.TryParse(dom("span.collection-carousel-overlay-top").Text().Trim().ToLower().Replace("episode ", String.Empty), episodeNumber) Then
                Return Nothing
            End If

            Dim scriptText = dom("script").Text().Trim()
            Dim bubbleData = scriptText.Substring(scriptText.IndexOf("{"), scriptText.LastIndexOf("}") - scriptText.IndexOf("{") + 1)
            Dim crEpisode As CrunchyrollEpisode = Nothing
            Try
                Using ms = New MemoryStream(Encoding.UTF8.GetBytes(bubbleData.ToCharArray()))
                    Dim ser = New DataContractJsonSerializer(GetType(CrunchyrollEpisode))
                    crEpisode = DirectCast(ser.ReadObject(ms), CrunchyrollEpisode)
                    crEpisode.SeasonNumber = CInt(SeasonNumber)
                    crEpisode.EpisodeNumber = episodeNumber
                End Using
            Catch ex As Exception
            End Try

            Dim FileName = episodeNumber.ToString("00") & ".jpg"
            Dim localPath = IO.Path.Combine(DownloadFolder, FileName)
            If Not IO.Directory.Exists(DownloadFolder) Then
                IO.Directory.CreateDirectory(DownloadFolder)
            End If
            MyBase.DownloadImageAddResult(imgSrc, localPath)

            'If Not IO.File.Exists(localPath) Then
            '    Try
            '        _client.DownloadFile(imgSrc, localPath)
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileName, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileName, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileName, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If
            Return crEpisode
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           If IO.Directory.Exists(DownloadFolder) Then
                                                               Process.Start(DownloadFolder)
                                                           End If
                                                       End Sub)
            End Get
        End Property

        Public Shared ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"full", "fwide"})
            End Get
        End Property

    End Class

End Namespace
