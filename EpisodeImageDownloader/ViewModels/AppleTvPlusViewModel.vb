Imports System.Reflection
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports EpisodeImageDownloader.DataStructures
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    <DataContract>
    Public Class AppleTvPlusViewModel
        Inherits ViewModelBase

        Public Sub New()
            Country = CountryInfos.First()
        End Sub

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
                    Dim match = Text.RegularExpressions.Regex.Match(value, "(umc\.cmc\.\w{20,})")
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

        Private _ImageSize As String = "Max"
        <DataMember(Order:=4, EmitDefaultValue:=False), ComponentModel.DefaultValue("Max")>
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
                Return New List(Of String)(New String() {"Max", "1920x1080", "1024x640"})
            End Get
        End Property

        Private _country As AppleCountryInformation
        <DataMember(Order:=5)>
        Public Property Country As AppleCountryInformation
            Get
                Return _country
            End Get
            Set(value As AppleCountryInformation)
                SetProperty(_country, value)
            End Set
        End Property

        'Apple Country Codes & Storefront IDs: https://affiliate.itunes.apple.com/resources/documentation/linking-to-the-itunes-music-store/#appendix
        '  https://gist.github.com/theiostream/5871770
        Public Shared ReadOnly Property CountryInfos As List(Of AppleCountryInformation)
            Get
                Return New List(Of AppleCountryInformation) From {
                    New AppleCountryInformation() With {.Name = "USA", .CountryCode = "us", .Locale = "en-US", .StorefrontID = 143441},
                    New AppleCountryInformation() With {.Name = "Australia", .CountryCode = "au", .Locale = "en-AU", .StorefrontID = 143460},
                    New AppleCountryInformation() With {.Name = "Brazil", .CountryCode = "br", .Locale = "pt-BR", .StorefrontID = 143503},
                    New AppleCountryInformation() With {.Name = "Canada", .CountryCode = "ca", .Locale = "en-CA", .StorefrontID = 143455},
                    New AppleCountryInformation() With {.Name = "France", .CountryCode = "fr", .Locale = "fr-FR", .StorefrontID = 143442},
                    New AppleCountryInformation() With {.Name = "Germany", .CountryCode = "de", .Locale = "de-DE", .StorefrontID = 143443},
                    New AppleCountryInformation() With {.Name = "Greece", .CountryCode = "gr", .Locale = "el-GR", .StorefrontID = 143448},
                    New AppleCountryInformation() With {.Name = "Lebanon", .CountryCode = "lb", .Locale = "ar-LB", .StorefrontID = 143497},
                    New AppleCountryInformation() With {.Name = "Mexico", .CountryCode = "mx", .Locale = "es-MX", .StorefrontID = 143468},
                    New AppleCountryInformation() With {.Name = "Netherlands", .CountryCode = "nl", .Locale = "nl-NL", .StorefrontID = 143452},
                    New AppleCountryInformation() With {.Name = "Norway", .CountryCode = "no", .Locale = "no-NO", .StorefrontID = 143457},
                    New AppleCountryInformation() With {.Name = "Portugal", .CountryCode = "pt", .Locale = "pt-PT", .StorefrontID = 143453},
                    New AppleCountryInformation() With {.Name = "Russia", .CountryCode = "ru", .Locale = "ru-RU", .StorefrontID = 143469},
                    New AppleCountryInformation() With {.Name = "Spain", .CountryCode = "es", .Locale = "es-ES", .StorefrontID = 143454},
                    New AppleCountryInformation() With {.Name = "UK", .CountryCode = "gb", .Locale = "en-GB", .StorefrontID = 143444}
                }
            End Get
        End Property

        Private _skipMainSeasonImgs As Boolean = False
        <DataMember(Order:=6, EmitDefaultValue:=False), ComponentModel.DefaultValue(False)>
        Public Property SkipMainSeasonImgs As Boolean
            Get
                Return _skipMainSeasonImgs
            End Get
            Set(value As Boolean)
                SetProperty(_skipMainSeasonImgs, value)
            End Set
        End Property

        Private _skipEpisodeImgs As Boolean = False
        <DataMember(Order:=7, EmitDefaultValue:=False), ComponentModel.DefaultValue(False)>
        Public Property SkipEpisodeImgs As Boolean
            Get
                Return _skipEpisodeImgs
            End Get
            Set(value As Boolean)
                SetProperty(_skipEpisodeImgs, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowId)
        End Function

        Protected Overrides Sub DownloadImages()
            If Not SkipMainSeasonImgs Then
                'Main Images
                Dim apiUrlFormat = "https://tv.apple.com/api/uts/v2/view/show/{0}?utsk=6e3013c6d6fae3c2%3A%3A%3A%3A%3A%3A235656c069bb0efb&caller=web&sf={1}&v=36&pfm=web&locale={2}"
                Dim apiUrl = String.Format(Globalization.CultureInfo.InvariantCulture, apiUrlFormat, ShowId, Country.StorefrontID, Country.Locale)
                Dim json As String = String.Empty
                Dim flags As BindingFlags = BindingFlags.Public Or BindingFlags.Instance
                Try
                    json = WebResources.DownloadString(apiUrl)
                Catch ex As Exception
                    MessageWindow.ShowDialog("Error downloading main show information: " & apiUrl, "Error getting main show info")
                End Try
                Dim mainShowInfo = json.FromJSON(Of AppleTvShowMainInfo)()
                If mainShowInfo IsNot Nothing Then
                    If String.IsNullOrWhiteSpace(ShowName) Then
                        ShowName = mainShowInfo.Data.Content.Title
                    End If
                    Dim type As Type = mainShowInfo.Data.Content.Images.GetType()
                    Dim properties As PropertyInfo() = type.GetProperties(flags)
                    'For Each [property] As PropertyInfo In properties
                    '    DownloadMainImages([property], mainShowInfo.Data.Content.Images)
                    'Next
                    Parallel.ForEach(properties, Sub(prop As PropertyInfo)
                                                     DownloadMainImage(prop, mainShowInfo.Data.Content.Images)
                                                 End Sub)
                End If

                'Season Images
                apiUrlFormat = "https://tv.apple.com/api/uts/v2/view/show/{0}/episodes?utsk=6e3013c6d6fae3c2%3A%3A%3A%3A%3A%3A235656c069bb0efb&caller=web&sf={1}&v=36&pfm=web&locale={2}"
                apiUrl = String.Format(Globalization.CultureInfo.InvariantCulture, apiUrlFormat, ShowId, Country.StorefrontID, Country.Locale)
                Try
                    json = WebResources.DownloadString(apiUrl)
                Catch ex As Exception
                    MessageWindow.ShowDialog("Error downloading show information: " & apiUrl, "Error getting show info")
                End Try
                Dim showInfo = json.FromJSON(Of AppleTvPlusShowData)()
                If String.IsNullOrWhiteSpace(ShowName) Then
                    ShowName = showInfo.Data.Seasons(0).ShowTitle
                End If
                If showInfo IsNot Nothing AndAlso showInfo.Data IsNot Nothing AndAlso showInfo.Data.Seasons IsNot Nothing Then
                    'For Each season In showInfo.Data.Seasons
                    '    Dim type As Type = season.Images.GetType()
                    '    Dim properties As PropertyInfo() = type.GetProperties(flags)

                    '    'For Each [property] As PropertyInfo In properties
                    '    '    DownloadSeasonImage([property], season)
                    '    'Next
                    '    Parallel.ForEach(properties, Sub(prop As PropertyInfo)
                    '                                     DownloadSeasonImage(prop, season)
                    '                                 End Sub)
                    'Next
                    Parallel.ForEach(showInfo.Data.Seasons,
                                     Sub(season As AppleTvPlusSeason)
                                         Dim type As Type = season.Images.GetType()
                                         Dim properties As PropertyInfo() = type.GetProperties(flags)

                                         'For Each [property] As PropertyInfo In properties
                                         '    DownloadSeasonImage([property], season)
                                         'Next
                                         Parallel.ForEach(properties,
                                                          Sub(prop As PropertyInfo)
                                                              DownloadSeasonImage(prop, season)
                                                          End Sub)
                                     End Sub)
                End If
            End If

            'Episodes
            Dim episodesUrlFormat = "https://tv.apple.com/api/uts/v2/view/show/{0}/episodes?skip=0&count=10000&utsk=6e3013c6d6fae3c2%3A%3A%3A%3A%3A%3A235656c069bb0efb&caller=web&sf={1}&v=36&pfm=web&locale={2}"
            Dim episodesUrl = String.Format(Globalization.CultureInfo.InvariantCulture, episodesUrlFormat, ShowId, Country.StorefrontID, Country.Locale)
            Dim epJson = String.Empty
            Try
                epJson = WebResources.DownloadString(episodesUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading episode information: " & episodesUrl, "Error getting episode info")
                NotBusy = True
                Exit Sub
            End Try
            Dim epInfo = epJson.FromJSON(Of AppleTvPlusShowData)()
            If String.IsNullOrWhiteSpace(ShowName) Then
                ShowName = epInfo.Data.Episodes(0).ShowTitle
            End If


            Dim episodesBag As New Concurrent.ConcurrentBag(Of TvDataEpisode)
            'If epInfo IsNot Nothing AndAlso epInfo.Data.Episodes IsNot Nothing Then
            '    For Each ep In epInfo.Data.Episodes
            '        If Not SeasonNumber.HasValue OrElse ep.SeasonNumber = SeasonNumber.Value Then
            '            DownloadEpisodeImage(ep, episodesBag)
            '        End If
            '    Next
            'End If

            Parallel.ForEach(epInfo.Data.Episodes,
                             Sub(ep As AppleTvPlusEpisode)
                                 If Not SeasonNumber.HasValue OrElse ep.SeasonNumber = SeasonNumber.Value Then
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
                tvdata.SaveToFile(ShowDownloadFolder, ShowName & fileSuffix & "_" & Country.CountryCode)
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If

        End Sub

        Private Sub DownloadSeasonImage(propInfo As PropertyInfo, season As AppleTvPlusSeason)
            Dim imageTypeName = propInfo.Name
            Dim imgInfo As AppleTvPlusImageInformation = CType(propInfo.GetValue(season.Images), AppleTvPlusImageInformation)
            If imgInfo Is Nothing Then
                Exit Sub
            End If
            Dim extension = ".jpg"
            If imageTypeName.ToUpperInvariant().Contains("LOGO") Then
                extension = ".png"
            End If
            Dim filename = "Season" & season.SeasonNumber & "_" & imageTypeName & "_" & Country.CountryCode
            DownloadImageAddResult(imgInfo.GetMaxUrl(extension.Replace(".", String.Empty)),
                                   IO.Path.Combine(SeasonDownloadFolder(season.SeasonNumber), filename & extension),
                                   filename)
        End Sub

        Private Sub DownloadMainImage(propInfo As PropertyInfo, imgs As AppleTvPlusImages)
            Dim imageTypeName = propInfo.Name
            Dim imgInfo As AppleTvPlusImageInformation = CType(propInfo.GetValue(imgs), AppleTvPlusImageInformation)
            If imgInfo Is Nothing Then
                Exit Sub
            End If
            Dim extension = ".jpg"
            If imageTypeName.ToUpperInvariant().Contains("LOGO") Then
                extension = ".png"
            End If
            Dim filename = imageTypeName & "_" & Country.CountryCode
            DownloadImageAddResult(imgInfo.GetMaxUrl(extension.Replace(".", String.Empty)),
                                   IO.Path.Combine(ShowDownloadFolder, filename & extension),
                                   filename)
        End Sub

        <CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification:="<Pending>")>
        Private Sub DownloadEpisodeImage(ep As AppleTvPlusEpisode, episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode))
            Dim tvDataEp As New TvDataEpisode() With {
                .EpisodeName = ep.Title,
                .Overview = ep.Description,
                .SeasonNumber = ep.SeasonNumber,
                .EpisodeNumber = ep.EpisodeNumber
                }
            If ep.ReleaseDate.HasValue Then
                tvDataEp.FirstAired = DateTimeOffset.FromUnixTimeMilliseconds(ep.ReleaseDate.Value).Date.ToIso8601DateString()
            End If
            episodesBag.Add(tvDataEp)

            Dim localFileName = "S" & ep.SeasonNumber.ToString("00") & "E" & ep.EpisodeNumber.ToString("00") & ".jpg"
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(ep.SeasonNumber), localFileName)
            If Not SkipEpisodeImgs Then
                'TODO: Create an overvload of DownloadImageAddResult that accepts Uri
                If ep.Images.PreviewFrame IsNot Nothing Then
                    DownloadImageAddResult(ep.Images.PreviewFrame.GetMaxUrl().ToString(), localPath)
                End If
            Else
                AddEpisodeImageResult(New EpisodeImageResult() With {
                                      .FileName = localFileName,
                                      .HasError = True,
                                      .NewDownload = False,
                                      .Message = "Image Download Skipped"})
            End If
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
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(AppleTvPlusViewModel))
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
                        Dim vm As New AppleTvPlusViewModel()
                        Using stream As New IO.MemoryStream(Text.Encoding.UTF8.GetBytes(My.Computer.FileSystem.ReadAllText(SeasonInfoFileName)))
                            Dim jsonSerializer As New DataContractJsonSerializer(GetType(AppleTvPlusViewModel))
                            vm = CType(jsonSerializer.ReadObject(stream), AppleTvPlusViewModel)
                        End Using
                        If vm IsNot Nothing AndAlso Not String.IsNullOrEmpty(vm.ShowId) Then
                            ShowId = vm.ShowId
                            SeasonNumber = vm.SeasonNumber
                            Country = vm.Country
                            ImageSize = vm.ImageSize
                            SkipMainSeasonImgs = vm.SkipMainSeasonImgs
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
                        ImageSize = "Max"
                        SkipEpisodeImgs = False
                        SkipMainSeasonImgs = False
                        Application.Current.Dispatcher.BeginInvoke(
                            Sub()
                                EpisodeImageResults.Clear()
                            End Sub)
                    End Sub)
            End Get
        End Property

    End Class

    Public Class AppleCountryInformation

        Public Property Name As String
        Public Property CountryCode As String
        Public Property StorefrontID As Integer
        Public Property Locale As String

    End Class

End Namespace