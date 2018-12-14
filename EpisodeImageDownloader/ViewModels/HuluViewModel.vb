Imports System.IO
Imports System.Text
Imports System.Runtime.Serialization.Json
Imports System.Xml.Serialization

Namespace ViewModels

    Public Class HuluViewModel
        Inherits ViewModels.ViewModelBase

        Private _client As Infrastructure.ChromeWebClient

        Public Sub New()
            IsJsonInputVisible = True
            ShowName = String.Empty
            ImageSize = "640x360"
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

        Private _imageSize As String
        Public Property ImageSize As String
            Get
                Return _imageSize
            End Get
            Set(value As String)
                SetProperty(_imageSize, value)
            End Set
        End Property

        Private _jsonText As String
        Public Property JsonText As String
            Get
                Return _jsonText
            End Get
            Set(value As String)
                SetProperty(_jsonText, value)
            End Set
        End Property

        Private _isJsonInputVisible As Boolean
        Public Property IsJsonInputVisible As Boolean
            Get
                Return _isJsonInputVisible
            End Get
            Set(value As Boolean)
                SetProperty(_isJsonInputVisible, value)
                OnPropertyChanged("IsJsonInputNotVisible")
            End Set
        End Property

        Public ReadOnly Property IsJsonInputNotVisible As Boolean
            Get
                Return Not _isJsonInputVisible
            End Get
        End Property

        Public ReadOnly Property DownloadFolder() As String
            Get
                Dim shownameSafe = ShowName.MakeFileNameSafe()
                If Not String.IsNullOrWhiteSpace(shownameSafe) Then
                    shownameSafe = shownameSafe.Substring(0, Math.Min(shownameSafe.Length, 40))
                End If

                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       shownameSafe)
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(JsonText)
        End Function

        Protected Overrides Sub DownloadImages()
            IsJsonInputVisible = False

            Dim huluEps As New HuluEpisodes()

            Try
                Using ms = New MemoryStream(Encoding.UTF8.GetBytes(JsonText.ToCharArray()))
                    Dim ser = New DataContractJsonSerializer(GetType(HuluEpisodes))
                    huluEps = DirectCast(ser.ReadObject(ms), HuluEpisodes)
                End Using
            Catch ex As Exception
                MessageWindow.ShowDialog(ex.Message, "Error parsing Json")
                NotBusy = True
                Exit Sub
            End Try

            If huluEps.Data IsNot Nothing Then
                _client = New Infrastructure.ChromeWebClient()
                Dim tvData As New TvDataSeries()
                For Each vidDatum In huluEps.Data
                    tvData.Episodes.Add(New TvDataEpisode() With {.SeasonNumber = vidDatum.Video.SeasonNumber,
                                                                  .EpisodeNumber = vidDatum.Video.EpisodeNumber,
                                                                  .EpisodeName = vidDatum.Video.Title,
                                                                  .FirstAired = vidDatum.Video.FirstAired,
                                                                  .Overview = vidDatum.Video.Description})
                    DownloadImage(vidDatum.Video)
                Next
                _client.Dispose()
                tvData.SaveToFile(DownloadFolder, ShowName)
            Else
                MessageWindow.ShowDialog("No videos found in Json", "No Videos")
            End If

        End Sub

        Private Sub DownloadImage(vid As HuluVideo)
            Dim filename = "S" & vid.SeasonNumber.ToString("00") & "E" & vid.EpisodeNumber.ToString("000") & "_" & vid.Title.MakeFileNameSafeNoSpaces() & "_" & ImageSize & ".jpg"
            Dim localPath = IO.Path.Combine(DownloadFolder, "Season " & vid.SeasonNumber.ToString("00"), filename)
            MyBase.DownloadImageAddResult(vid.ImageUrl(ImageSize), localPath)

            'If Not IO.File.Exists(localPath) Then
            '    If Not IO.Directory.Exists(IO.Path.GetDirectoryName(localPath)) Then
            '        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(localPath))
            '    End If
            '    Try
            '        _client.DownloadFile(vid.ImageUrl(ImageSize), localPath)
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If
        End Sub

        'Private Sub SaveTvDataXml(tvData As TvDataSeries)
        '    Dim savePath As String = IO.Path.Combine(DownloadFolder, ShowName & ".tvxml")
        '    If Not IO.Directory.Exists(DownloadFolder) Then
        '        IO.Directory.CreateDirectory(DownloadFolder)
        '    End If
        '    If IO.File.Exists(savePath) Then
        '        If MessageWindow.ShowDialog("File " & savePath & " exists." & Environment.NewLine & Environment.NewLine & "Overwrite existing File?",
        '                                    "Overwrite existing?", True) = False Then
        '            Exit Sub
        '        End If
        '    End If
        '    Using objStreamWriter As New StreamWriter(savePath)
        '        Dim x As New XmlSerializer(tvData.GetType())
        '        x.Serialize(objStreamWriter, tvData)
        '        objStreamWriter.Close()
        '    End Using
        'End Sub

        Public Function DownloadFolderExists() As Boolean
            Return IO.Directory.Exists(DownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           If IO.Directory.Exists(DownloadFolder) Then
                                                               Process.Start(DownloadFolder)
                                                           End If
                                                       End Sub, AddressOf DownloadFolderExists)
            End Get
        End Property

        Public ReadOnly Property ShowJsonInputCommand() As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           IsJsonInputVisible = True
                                                       End Sub)
            End Get
        End Property

    End Class

End Namespace
