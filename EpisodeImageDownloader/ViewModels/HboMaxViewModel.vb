Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports EpisodeImageDownloader.DataStructures
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    <DataContract>
    Public Class HboMaxViewModel
        Inherits ViewModelBase

        Private Const ApiUrlFormat = "https://comet.api.hbo.com/express-content/{0}?device-code=desktop&product-code=hboMax&api-version=v9&country-code=us&profile-type=default&signed-in=true"

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
                    Dim match = Text.RegularExpressions.Regex.Match(value, "(urn:hbo:(?:series|franchise):[\w:-]+)")
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

        Private _ImageSize As String = "3840x2160"
        <DataMember(Order:=4, EmitDefaultValue:=False), ComponentModel.DefaultValue("3840x2160")>
        Public Property ImageSize As String
            Get
                Return _ImageSize
            End Get
            Set(value As String)
                SetProperty(_ImageSize, value)
            End Set
        End Property

        Public Shared ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"3840x2160", "1920x1080", "1280x720", "1024x640"})
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowId)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim apiUrl = String.Format(Globalization.CultureInfo.InvariantCulture, ApiUrlFormat, ShowId)
            Dim json As String = String.Empty
            Try
                json = WebResources.DownloadString(apiUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading show information: " & apiUrl, "Error getting show info")
            End Try
            Dim data As List(Of HboMaxDatum) = json.FromJSONArray(Of HboMaxDatum)().ToList()
            'Get the show name if user left it blank
            If String.IsNullOrWhiteSpace(ShowName) Then
                Dim seriesDatum = data.Where(Function(d) d.Id.Contains(":series:")).First()
                If seriesDatum Is Nothing Then
                    MessageBox.Show("Could not find series name in returned JSON", "Error")
                    Exit Sub
                End If
                ShowName = seriesDatum.Body.Titles.Full
            End If

            Dim eps = data.Where(Function(d) d.Id.Contains(":episode:") AndAlso Not d.Id.Contains(":edit:")).ToList()

            Dim episodesBag As New Concurrent.ConcurrentBag(Of TvDataEpisode)

            'For Each ep In eps
            '    If Not SeasonNumber.HasValue OrElse ep.Body.SeasonNumber = SeasonNumber.Value Then
            '        DownloadEpisodeImage(ep, episodesBag)
            '    End If
            'Next
            Parallel.ForEach(eps,
                             Sub(ep As HboMaxDatum)
                                 If Not SeasonNumber.HasValue OrElse ep.Body.SeasonNumber = SeasonNumber.Value Then
                                     DownloadEpisodeImage(ep, episodesBag)
                                 End If
                             End Sub)

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

        <CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification:="<Pending>")>
        Private Sub DownloadEpisodeImage(ep As HboMaxDatum, episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode))
            Dim tvDataEp As New TvDataEpisode() With {
               .EpisodeName = ep.Body.Titles.Full,
               .Overview = ep.Body.Summaries.Full,
               .FirstAired = ep.Body.FirstOfferedDate.ToIso8601DateString()
               }

            If ep.Body.SeasonNumber.HasValue Then
                tvDataEp.SeasonNumber = ep.Body.SeasonNumber.Value
            Else
                tvDataEp.SeasonNumber = 1
            End If

            If ep.Body.NumberInSeason.HasValue Then
                tvDataEp.EpisodeNumber = ep.Body.NumberInSeason.Value
            ElseIf ep.Body.NumberInSeries.HasValue Then
                tvDataEp.EpisodeNumber = ep.Body.NumberInSeries.Value
            End If
            episodesBag.Add(tvDataEp)
            Dim localFileName = "S" & tvDataEp.SeasonNumber.ToString("00") & "E" & tvDataEp.EpisodeNumber.ToString("00") & ".jpg"
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(tvDataEp.SeasonNumber), localFileName)

            Dim imgRegex = New Text.RegularExpressions.Regex("{{size}}[^""]+", Text.RegularExpressions.RegexOptions.IgnoreCase)

            DownloadImageAddResult(imgRegex.Replace(ep.Body.Images.Tile, ImageSize), localPath)

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

        Public Function SeasonInfoFileName() As String
            Return IO.Path.Combine(ShowDownloadFolder, ShowName.MakeFileNameSafe & ".eid")
        End Function

        Public Function SeasonInfoFileExists() As Boolean
            Return IO.File.Exists(SeasonInfoFileName)
        End Function

        Public ReadOnly Property SaveSeasonInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        If Not IO.Directory.Exists(ShowDownloadFolder) Then
                            IO.Directory.CreateDirectory(ShowDownloadFolder)
                        End If
                        ' if file has a greater length (of characters) than what is about to be written, the file will contain
                        '  a mix of the two files.  WriteAllText will clear the file first to overcome this.
                        If IO.File.Exists(SeasonInfoFileName) Then
                            IO.File.WriteAllText(SeasonInfoFileName, String.Empty)
                        End If
                        Using siFile As IO.FileStream = IO.File.OpenWrite(SeasonInfoFileName)
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(HboMaxViewModel))
                            jsonSerializer.WriteObject(siFile, Me)
                        End Using
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse
                               (CanDownloadImages() AndAlso Not String.IsNullOrWhiteSpace(ShowName))
                    End Function)
            End Get
        End Property

        Public ReadOnly Property LoadSeasonInfoCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        Dim vm As New HboMaxViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(HboMaxViewModel))
                            vm = CType(jsonSerializer.ReadObject(stream), HboMaxViewModel)
                        End Using
                        If vm IsNot Nothing AndAlso Not String.IsNullOrEmpty(vm.ShowId) Then
                            ShowId = vm.ShowId
                            SeasonNumber = vm.SeasonNumber
                            ImageSize = vm.ImageSize
                        Else
                            MessageWindow.ShowDialog("Unable to read configuration file " & SeasonInfoFileName(), "Error reading config")
                        End If
                    End Sub,
                    Function() As Boolean
                        Return ComponentModel.DesignerProperties.GetIsInDesignMode(New DependencyObject) OrElse SeasonInfoFileExists()
                    End Function)
            End Get
        End Property

#End Region

        Public ReadOnly Property ClearInputsCommand As ICommand
            Get
                Return New RelayCommand(
                    Sub()
                        ShowId = String.Empty
                        ShowName = String.Empty
                        SeasonNumber = Nothing
                        ImageSize = "3840x2160"
                        Application.Current.Dispatcher.BeginInvoke(
                            Sub()
                                EpisodeImageResults.Clear()
                            End Sub)
                    End Sub)
            End Get
        End Property

    End Class

End Namespace
