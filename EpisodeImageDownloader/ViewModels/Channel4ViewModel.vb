Imports System.Runtime.Serialization

Namespace ViewModels

    Public Class Channel4ViewModel
        Inherits ViewModelBase

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

        Private _showUrl As String
        Public Property ShowUrl As String
            Get
                Return _showUrl
            End Get
            Set(value As String)
                SetProperty(_showUrl, value)
            End Set
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
                   Not String.IsNullOrWhiteSpace(ShowUrl)
        End Function

        'Public Sub Test(Name As String, Url As String)
        '    ShowName = Name
        '    ShowUrl = Url
        '    DownloadImages()
        'End Sub

        Protected Overrides Sub DownloadImages()
            ParseEpisodes()
        End Sub

        Private Sub ParseEpisodes()
            Dim html As String
            Try
                html = Infrastructure.WebResources.DownloadString(ShowUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading html:" & ex.Message, "Error")
                Exit Sub
            End Try
            ' past episodes
            Dim showDataMatch = Text.RegularExpressions.Regex.Match(html, "onDemand.selectedSeries\s=\s(\{.+});")
            Dim showData As New Channel4ShowData()
            If showDataMatch.Success Then
                showData = showDataMatch.Groups(1).Value.FromJSON(Of Channel4ShowData)()
            End If
            ' coming soon episodes
            Dim comingSoonMatch = Text.RegularExpressions.Regex.Match(html, "comingSoon"":({.+}),")
            If comingSoonMatch.Success Then
                Dim comingSoonEpisode = comingSoonMatch.Groups(1).Value.FromJSON(Of Channel4Episode)()
                showData.Episodes.Add(comingSoonEpisode)
            End If
            ' watch live episodes
            Dim watchLiveMatch = Text.RegularExpressions.Regex.Match(html, "onDemand\.watchLive\s=\s{""watchLiveSchedule"":(.+)}")
            If watchLiveMatch.Success Then
                Dim watchLiveEpisodes = watchLiveMatch.Groups(1).Value.FromJSONArray(Of Channel4Episode)()
                For Each ep In watchLiveEpisodes
                    If showData.Episodes.Where(Function(e) e.EpisodeNumber = ep.EpisodeNumber AndAlso
                                                           e.SeasonNumber = ep.SeasonNumber).Count = 0 Then
                        showData.Episodes.Add(ep)
                    End If
                Next
            End If

            ''Parallel.ForEach(showData.Episodes, AddressOf DownloadEpisodeImage)

            Dim tvData As New TvDataSeries()
            For Each ep In showData.Episodes
                DownloadEpisodeImage(ep)
                tvData.Episodes.Add(New TvDataEpisode() With {.EpisodeName = ep.TitleNonNull,
                                                              .EpisodeNumber = ep.EpisodeNumber,
                                                              .FirstAired = ep.FirstAired,
                                                              .Overview = ep.Synopsis,
                                                              .SeasonNumber = ep.SeasonNumber})
            Next

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName)
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If

        End Sub

        Private Sub DownloadEpisodeImage(episode As Channel4Episode)
            'If Not IO.Directory.Exists(SeasonDownloadFolder(episode.SeasonNumber)) Then
            '    IO.Directory.CreateDirectory(SeasonDownloadFolder(episode.SeasonNumber))
            'End If

            Dim extension = IO.Path.GetExtension(episode.ImageFullSizeUrl)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.TitleNonNull.MakeFileNameSafe() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(episode.ImageFullSizeUrl, localPath)

            'If Not IO.File.Exists(localPath) Then
            '    Try
            '        Using client As New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
            '            client.DownloadFile(episode.ImageFullSizeUrl, localPath)
            '        End Using
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
            '    Catch ex As Exception
            '        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
            '    End Try
            'Else
            '    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            'End If
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

#Region " Data Structures "

        <DataContract>
        Public Class PictureComponent

            <DataMember(Name:="url")>
            Public Property Url As String

            <DataMember(Name:="altText")>
            Public Property AltText As String

            <DataMember(Name:="imageHeight")>
            Public Property ImageHeight As String

            <DataMember(Name:="imageWidth")>
            Public Property ImageWidth As String

            <DataMember(Name:="imageType")>
            Public Property ImageType As String
        End Class

        <DataContract>
        Public Class Channel4Episode

            <DataMember(Name:="seriesNumber")>
            Public Property SeasonNumber As Integer

            <DataMember(Name:="episodeNumber")>
            Public Property EpisodeNumber As Integer

            <DataMember(Name:="title1")>
            Public Property Title1 As String

            <DataMember(Name:="title2")>
            Public Property Title2 As String

            <DataMember(Name:="txChannel")>
            Public Property TxChannel As String

            <DataMember(Name:="txDate")>
            Public Property TxDate As String

            Public ReadOnly Property FirstAired As String
                Get
                    Return TxDate.ToIso8601DateString()
                End Get
            End Property

            <DataMember(Name:="txTime")>
            Public Property TxTime As String

            <DataMember(Name:="pictureComponent")>
            Public Property PictureComponent As PictureComponent

            <DataMember(Name:="episodeInfo")>
            Public Property EpisodeInfo As String

            <DataMember(Name:="synopsis")>
            Public Property Synopsis As String

            <DataMember(Name:="shortSynopsis")>
            Public Property ShortSynopsis As String

            <DataMember(Name:="title3")>
            Public Property Title3 As Object

            <DataMember(Name:="brandTitle")>
            Public Property BrandTitle As String

            <DataMember(Name:="episodeTitle")>
            Public Property EpisodeTitle As String

            <DataMember(Name:="imageUrl")>
            Public Property ImageUrl As String

            Public ReadOnly Property ImageFullSizeUrl As String
                Get
                    Dim pictureUrl = ImageUrl
                    If String.IsNullOrEmpty(pictureUrl) Then
                        pictureUrl = PictureComponent.Url
                    End If
                    Return New Uri(pictureUrl).GetLeftPart(UriPartial.Path)
                End Get
            End Property

            Public ReadOnly Property TitleNonNull As String
                Get
                    If Not String.IsNullOrEmpty(Me.EpisodeTitle) Then
                        Return Me.EpisodeTitle
                    ElseIf Not String.IsNullOrEmpty(Title1) Then
                        Return Title1
                    Else
                        Return Title2
                    End If
                End Get
            End Property

        End Class

        <DataContract>
        Public Class Channel4ShowData

            <DataMember(Name:="episodeCount")>
            Public Property EpisodeCount As Integer

            <DataMember(Name:="url")>
            Public Property Url As String

            <DataMember(Name:="episodes")>
            Public Property Episodes As List(Of Channel4Episode)

            <DataMember(Name:="archive")>
            Public Property Archive As Boolean

        End Class

#End Region

    End Class

End Namespace