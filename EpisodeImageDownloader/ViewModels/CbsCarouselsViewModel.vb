Imports System.Runtime.Serialization
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class CbsCarouselsViewModel
        Inherits ViewModels.ViewModelBase

        Private tvData As TvDataSeries
        Private imageType As String = "section"

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

        Private _sectionId As Integer?
        Public Property SectionId As Integer?
            Get
                Return _sectionId
            End Get
            Set(value As Integer?)
                SetProperty(_sectionId, value)
            End Set
        End Property

        Private _showId As Integer?
        Public Property ShowId As Integer?
            Get
                Return _showId
            End Get
            Set(value As Integer?)
                SetProperty(_showId, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                    SeasonNumber.HasValue AndAlso
                    (ShowId.HasValue Or SectionId.HasValue)
        End Function

        Protected Overrides Sub DownloadImages()
            tvData = New TvDataSeries()

            Dim GetUrl As GetUrlDelegate = AddressOf GetSectionUrl
            imageType = "section"
            If Not SectionId.HasValue Then
                GetUrl = AddressOf GetShowUrl
                imageType = "show"
            End If

            Dim offset As Integer = 0
            Dim limit As Integer = 15
            Dim size As Integer = 0
            Do
                My.Application.StatusMessage = String.Format("Downloading JSON for episodes {0} - {1}", offset + 1, offset + limit + 1)
                Dim json = WebResources.DownloadString(GetUrl(offset, limit))
                Dim response = json.FromJSON(Of CbsResponse)()
                size = response.Result.Size
                Dim validEps = response.Result.Data.Where(Function(e) e.Type = "Full Episode" AndAlso
                                                                  e.EpisodeNumber.IsInteger AndAlso
                                                                  e.SeasonNumber.IsInteger)
                My.Application.StatusMessage = String.Format("Downloading episode images {0} - {1} of {2}", offset + 1, offset + size + 1, response.Result.Total)

                'For Each ep In validEps
                '    DownloadEpisode(ep)
                'Next
                Parallel.ForEach(validEps, AddressOf DownloadEpisode)

                offset += limit
            Loop Until size < limit

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & SeasonNumber)
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
            My.Application.StatusMessage = String.Empty
        End Sub

        Private Sub DownloadEpisode(ep As EpisodeData)
            Dim extension = IO.Path.GetExtension(ep.Thumb.Large)
            Dim SeasonEpisodeFilename = "S" & CInt(ep.SeasonNumber).ToString("00") & "E" & CInt(ep.EpisodeNumber).ToString("000") & "_" & ep.EpisodeTitle.MakeFileNameSafe() & "_" & imageType & extension
            Dim localPath = IO.Path.Combine(ShowDownloadFolder, "Season " & CInt(SeasonNumber).ToString("00"), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(ep.Thumb.Large, localPath)
            tvData.Episodes.Add(ep.ToTvDataEpisode)
        End Sub

#Region " Commands "

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder)
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

#End Region

#Region " Helper Functions "

        Private Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                   ShowName.MakeFileNameSafe)
            Else
                Return String.Empty
            End If
        End Function

        Private Delegate Function GetUrlDelegate(offset As Integer, limit As Integer) As String

        Private Function GetSectionUrl(offset As Integer, limit As Integer) As String
            If Not SectionId.HasValue Then
                Throw New NullReferenceException("SectionId cannot be Nothing when calling GetSectionUrl")
            End If
            'https://www.cbs.com/carousels/videosBySection/238069/offset/0/limit/15/xs/0/45/
            Const format = "https://www.cbs.com/carousels/videosBySection/{0}/offset/{1}/limit/{2}/xs/0/{3}/"
            Return String.Format(format, SectionId, offset, limit, SeasonNumber)
        End Function

        Private Function GetShowUrl(offset As Integer, limit As Integer) As String
            If Not ShowId.HasValue Then
                Throw New NullReferenceException("ShowId cannot be Nothing when calling GetShowUrl")
            End If
            'https://www.cbs.com/carousels/shows/617/offset/16/limit/8/xs/0/45/
            Const format = "https://www.cbs.com/carousels/shows/{0}/offset/{1}/limit/{2}/xs/0/{3}/"
            Return String.Format(format, ShowId, offset, limit, SeasonNumber)
        End Function

#End Region

#Region " Data Structures "

        <DataContract>
        Public Class Thumb

            <DataMember(Name:="large")>
            Public Property Large As String

            <DataMember(Name:="small")>
            Public Property Small As String

            <DataMember(Name:="640x360")>
            Public Property _640x360 As String

            <DataMember(Name:="640x480")>
            Public Property _640x480 As String
        End Class

        <DataContract>
        Public Class EpisodeData

            '' "Full Episode"
            <DataMember(Name:="type")>
            Public Property Type As String

            '<DataMember(Name:="title")>
            'Public Property Title As String

            <DataMember(Name:="series_title")>
            Public Property SeriesTitle As String

            <DataMember(Name:="label")>
            Public Property Label As String

            <DataMember(Name:="airdate")>
            Public Property Airdate As String

            <DataMember(Name:="airdate_ts")>
            Public Property AirdateTs As Object

            <DataMember(Name:="airdate_iso")>
            Public Property AirdateIso As String

            <DataMember(Name:="season_number")>
            Public Property SeasonNumber As String

            <DataMember(Name:="episode_number")>
            Public Property EpisodeNumber As String

            <DataMember(Name:="description")>
            Public Property Description As String

            <DataMember(Name:="thumb")>
            Public Property Thumb As Thumb

            <DataMember(Name:="url")>
            Public Property Url As String

            '<DataMember(Name:="show_id")>
            'Public Property ShowId As Integer

            <DataMember(Name:="episode_title")>
            Public Property EpisodeTitle As String

            Public Function ToTvDataEpisode() As TvDataEpisode
                Dim ep = New TvDataEpisode() With {
                .EpisodeName = EpisodeTitle,
                .EpisodeNumber = CInt(EpisodeNumber),
                .FirstAired = Airdate.ToIso8601DateString,
                .ImageUrl = Thumb.Large,
                .SeasonNumber = CInt(SeasonNumber)}
                If Not String.IsNullOrEmpty(Description) Then
                    ep.Overview = Text.RegularExpressions.Regex.Replace(Description, "\(TV.+\)", String.Empty).Trim()
                End If
                Return ep
            End Function
        End Class

        <DataContract>
        Public Class Result

            <DataMember(Name:="id")>
            Public Property Id As Integer

            <DataMember(Name:="title")>
            Public Property Title As String

            '<DataMember(Name:="layout")>
            'Public Property Layout As Integer

            <DataMember(Name:="total")>
            Public Property Total As Integer

            <DataMember(Name:="size")>
            Public Property Size As Integer

            <DataMember(Name:="has_full_episode")>
            Public Property HasFullEpisode As Boolean

            <DataMember(Name:="display_seasons")>
            Public Property DisplaySeasons As Boolean

            <DataMember(Name:="background_image")>
            Public Property BackgroundImage As String

            <DataMember(Name:="data")>
            Public Property Data As List(Of EpisodeData)
        End Class

        <DataContract>
        Public Class CbsResponse

            <DataMember(Name:="success")>
            Public Property Success As Boolean

            <DataMember(Name:="result")>
            Public Property Result As Result
        End Class

#End Region

    End Class

End Namespace
