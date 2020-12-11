Imports System.IO
Imports System.Text
Imports System.Runtime.Serialization.Json
Imports System.Xml.Serialization
Imports System.Runtime.Serialization
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class HuluViewModel
        Inherits ViewModels.ViewModelBase

        Private Const apiUrlFormat = "https://discover.hulu.com/content/v5/hubs/series/{0}/collections/94"

#Region " Properties "

        Private _showName As String
        <DataMember(Order:=1)>
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _showId As String
        <DataMember(Order:=2)>
        Public Property ShowId As String
            Get
                Return _showId
            End Get
            Set(value As String)
                If String.IsNullOrEmpty(value) Then
                    SetProperty(_showId, value)
                Else
                    Dim match = Text.RegularExpressions.Regex.Match(value, "series\/\w+-([\w-]+)")
                    If match.Success Then
                        SetProperty(_showId, match.Groups(1).Value)
                    End If
                End If
            End Set
        End Property

        Private _seasonNumber As Integer?
        <DataMember(Order:=3, EmitDefaultValue:=False), ComponentModel.DefaultValue(GetType(Integer?), Nothing)>
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
            Return Not String.IsNullOrWhiteSpace(ShowId)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim apiUrl = String.Format(Globalization.CultureInfo.InvariantCulture, apiUrlFormat, ShowId)
            Dim json As String
            Try
                Using client As New ChromeWebClient
                    client.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9"
                    json = client.DownloadString(apiUrl)
                End Using
                'json = WebResources.DownloadString(apiUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading episode information: " & apiUrl & Environment.NewLine & ex.Message, "Error getting episode info")
                NotBusy = True
                Exit Sub
            End Try
            Dim collection = json.FromJSON(Of HuluCollection)
            If String.IsNullOrWhiteSpace(ShowName) Then
                ShowName = collection.Items(0).SeriesName()
            End If
            Dim episodesBag As New Concurrent.ConcurrentBag(Of TvDataEpisode)

            For Each item In collection.Items
                If Not SeasonNumber.HasValue OrElse item.SeasonNumber.Value = SeasonNumber.Value Then
                    DownloadEpisodeImage(item, episodesBag)
                End If
            Next

            'Parallel.ForEach(collection.Items,
            '                 Sub(item As HuluCollectionItem)
            '                     If Not SeasonNumber.HasValue OrElse item.SeasonNumber.Value = SeasonNumber.Value Then
            '                         DownloadEpisodeImage(item, episodesBag)
            '                     End If
            '                 End Sub)

            If episodesBag.Count > 0 Then
                Dim tvdata As New TvDataSeries With {
                    .Episodes = episodesBag.OrderBy(Function(e) e.SeasonNumber).ThenBy(Function(e) e.EpisodeNumber).ToList()
                }
                Dim fileSuffix = String.Empty
                If SeasonNumber.HasValue Then
                    fileSuffix = " " & "Season " & SeasonNumber.Value.ToString("00", Globalization.CultureInfo.InvariantCulture)
                End If
                tvdata.SaveToFile(ShowDownloadFolder, ShowName & fileSuffix)
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If
        End Sub

        <CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification:="No Localization")>
        Private Sub DownloadEpisodeImage(ep As HuluCollectionItem, episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode))
            If Not ep.SeasonNumber.HasValue OrElse Not ep.EpisodeNumber.HasValue Then
                Exit Sub
            End If
            Dim tvDataEp As New TvDataEpisode() With {
                .EpisodeName = ep.Name,
                .Overview = ep.Description,
                .SeasonNumber = ep.SeasonNumber.Value,
                .EpisodeNumber = ep.EpisodeNumber.Value,
                .FirstAired = ep.PremiereDate.ToIso8601DateString
                }
            episodesBag.Add(tvDataEp)
            Dim localFileName = "S" & ep.SeasonNumber.Value.ToString("00") & "E" & ep.EpisodeNumber.Value.ToString("00") & ".jpg"
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(ep.SeasonNumber.Value), localFileName)
            DownloadImageAddResult(GetRemotePath(ep.Artwork.VideoHorizontalHero), localPath)
        End Sub

        Private Function GetRemotePath(artDetails As HuluArtworkDetails) As String
            Return artDetails.Path & "&size=" & artDetails.Width & "x" & artDetails.Height & "&format=jpeg"
        End Function


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
                Return New RelayCommand(
                    Sub()
                        Process.Start(ShowDownloadFolder())
                    End Sub,
                    AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Private Function SeasonDownloadFolder(seasonNo As Integer) As String
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & seasonNo.ToString("00", Globalization.CultureInfo.InvariantCulture))
        End Function

#End Region

#Region " Save/Load Season Infos "

        Public Function ConfigInfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName.MakeFileNameSafe & ".eid")
        End Function
        Public Function ConfigInfoFileExists() As Boolean
            Return IO.File.Exists(ConfigInfoFileName)
        End Function

        Public ReadOnly Property SaveConfigInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(ShowDownloadFolder) Then
                            IO.Directory.CreateDirectory(ShowDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(ConfigInfoFileName) Then
                            IO.File.WriteAllText(ConfigInfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(ConfigInfoFileName)
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(HuluViewModel))
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse
                               (CanDownloadImages() AndAlso Not String.IsNullOrWhiteSpace(ShowName))
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadConfigInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        Dim vm As New HuluViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(ConfigInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(HuluViewModel))
                            vm = CType(jsonSerializer.ReadObject(stream), HuluViewModel)
                        End Using
                        If vm IsNot Nothing AndAlso Not String.IsNullOrEmpty(vm.ShowId) Then
                            ShowId = vm.ShowId
                            SeasonNumber = vm.SeasonNumber
                        Else
                            MessageWindow.ShowDialog("Unable to read configuration file " & ConfigInfoFileName(), "Error reading config")
                        End If
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse ConfigInfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

    End Class

End Namespace
