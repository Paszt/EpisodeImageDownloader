Imports System.Collections.Specialized
Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class NbcEpisodesViewModel
        Inherits ViewModelBase

        Private data As TvDataSeries

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

        Private _oldestSeasonNumber As Integer?
        Public Property OldestSeasonNumber As Integer?
            Get
                Return _oldestSeasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_oldestSeasonNumber, value)
            End Set
        End Property

        Private _oldestEpisodeNumber As Integer?
        Public Property OldestEpisodeNumber As Integer?
            Get
                Return _oldestEpisodeNumber
            End Get
            Set(value As Integer?)
                SetProperty(_oldestEpisodeNumber, value)
            End Set
        End Property

        Private _statusMessage As String
        Public Property StatusMessage As String
            Get
                Return _statusMessage
            End Get
            Set(value As String)
                SetProperty(_statusMessage, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName)
        End Function

        Protected Overrides Sub DownloadImages()
            data = New TvDataSeries()
            Dim mainHtml = String.Empty

            Try
                mainHtml = WebResources.DownloadString(ShowUrl)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading main Html: " & ex.Message, "Error")
                Exit Sub
            End Try

            Dim showIdMatch = Regex.Match(mainHtml, """entities"":\{""([\w-]+)""", RegexOptions.IgnoreCase)
            If showIdMatch.Success Then
                Dim showId = showIdMatch.Groups(1).Value

                Dim currentSeasonNumberMatch = Regex.Match(mainHtml, """seasonNumber"":""(\d+)""", RegexOptions.IgnoreCase)
                If currentSeasonNumberMatch.Success Then
                    Dim currentSeasonNumber As Integer = CInt(currentSeasonNumberMatch.Groups(1).Value)
                    GetEpisodes(showId, currentSeasonNumber)
                Else
                    MessageWindow.ShowDialog("Failed to find current season number in downloaded html.", "No Season Number Found")
                End If
            Else
                MessageWindow.ShowDialog("Failed to find show ID in downloaded html.", "No Id Found")
            End If
        End Sub

        Private Sub GetEpisodes(showId As String, currentSeasonNumber As Integer)
            Dim json = String.Empty
            Dim pageSize As Integer = 20
            Dim apiUrl = ShowApiUrl(showId, currentSeasonNumber, pageSize)
            Dim nextUrl = String.Empty

            Using client As New ChromeWebClient With {.Accept = "application/vnd.api+json; ext=""park/derivatives"""}
                Do
                    json = client.DownloadString(apiUrl)
                    Dim NbcPage = json.FromJSON(Of NbcEpisodesPage)()
                    For Each ep In NbcPage.Data
                        Dim tvep = New TvDataEpisode() With {
                                          .EpisodeName = ep.Attributes.Title,
                                          .EpisodeNumber = CInt(ep.Attributes.EpisodeNumber),
                                          .FirstAired = ep.Attributes.Airdate.ToIso8601DateString,
                                          .ImageUrl = GetLargestImageUrl(EnsureAbsoluteUrl(NbcPage.GetEpisodeImageUrl(ep.Relationships.Image.Data.Id))),
                                          .Overview = ep.Attributes.Description,
                                          .SeasonNumber = CInt(ep.Attributes.SeasonNumber)}
                        If OldestSeasonNumber.HasValue Then
                            If tvep.SeasonNumber < OldestSeasonNumber.Value Then
                                Exit Do
                            Else
                                If OldestEpisodeNumber.HasValue AndAlso
                                    tvep.SeasonNumber = OldestSeasonNumber AndAlso
                                    tvep.EpisodeNumber < OldestEpisodeNumber.Value Then
                                    Exit Do
                                Else
                                    data.Episodes.Add(tvep)
                                    DownloadImageAddResult(tvep.ImageUrl, LocalEpisodeImagePath(tvep))
                                End If
                            End If
                        End If

                        apiUrl = NbcPage.Links.Next
                    Next
                Loop Until String.IsNullOrEmpty(apiUrl)
            End Using

            If data.Episodes.Count > 0 Then

                For Each seasonNo In (From e In data.Episodes Select e.SeasonNumber Distinct)
                    Dim tvd As New TvDataSeries With {
                        .Episodes = data.Episodes.Where(Function(e) e.SeasonNumber = seasonNo).OrderBy(Function(e) e.EpisodeNumber).ToList()}
                    tvd.SaveToFile(ShowDownloadFolder, ShowName & " Season " & seasonNo.ToString("00"))
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
        End Sub

#Region " Helper Functions "

        Private Function ShowUrl() As String
            Dim urlFormat = "http://www.nbc.com/{0}/episodes"
            Return String.Format(urlFormat, ShowName.ToSlug())
        End Function

        Private Function ShowApiUrl(showId As String, seasonNumber As Integer, pageSize As Integer) As String
            'Dim format = "https://api.nbc.com/v3.13/videos?fields%5Bvideos%5D=airdate%2Cavailable%2Cdescription%2Centitlement%2Cexpiration%2Cgenre%2Cguid%2CinternalId%2Ckeywords%2Cpermalink%2CrunTime%2Ctitle%2Ctype%2CvChipRating%2CembedUrl%2CseasonNumber%2CepisodeNumber%2CdayPart%2Ccredits&fields%5Bshows%5D=category%2Ccolors%2Cdescription%2CinternalId%2Cname%2Cnavigation%2Creference%2CschemaType%2CshortDescription%2CshortTitle%2CurlAlias%2CshowTag%2Csocial%2CtuneIn%2Ctype&fields%5Bseasons%5D=seasonNumber%2CcontestantTitle&fields%5Bimages%5D=derivatives&include=image%2Cshow%2Cshow.season&derivatives=landscape.widescreen.size350.x1%2Clandscape.widescreen.size640.x1%2Clandscape.widescreen.size640.x2&filter%5Bshow%5D={0}&filter%5Bavailable%5D%5Bvalue%5D=2017-05-12T14%3A30%3A00-04%3A00&filter%5Bavailable%5D%5Boperator%5D=%3C%3D&filter%5Btype%5D%5Bvalue%5D=Full%20Episode&filter%5Btype%5D%5Boperator%5D=%3D&filter%5BseasonNumber%5D=52&page%5Bnumber%5D={1}&page%5Bsize%5D=6&sort=-airdate"
            'Return String.Format(format, showId, page)

            ' ParseQueryString creates a writeable instance of an internal HttpValueCollection object,
            '    calling ToString method on that object will call the overridden method on HttpValueCollection, 
            '    which formats the collection as a URL-encoded query string.
            Dim queryString As NameValueCollection = Web.HttpUtility.ParseQueryString(String.Empty)
            queryString.Add("fields[videos]", "airdate,description,title,type,seasonNumber,episodeNumber")
            queryString.Add("fields[images]", "derivatives")
            queryString.Add("include", "image")
            queryString.Add("derivatives", "landscape.widescreen.size350.x1")
            queryString.Add("filter[show]", showId)
            queryString.Add("filter[available][value]", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK"))
            queryString.Add("filter[available][operator]", "<=")
            queryString.Add("filter[type][value]", "Full Episode")
            queryString.Add("filter[type][operator]", "=")
            queryString.Add("page[number]", "1")
            queryString.Add("page[size]", pageSize.ToString)
            queryString.Add("sort", "-airdate")
            Return "https://api.nbc.com/v3.13/videos?" & queryString.ToString
        End Function

        Private Function EnsureAbsoluteUrl(url As String) As String
            Dim theUri As Uri = Nothing
            If Uri.TryCreate(url, UriKind.Absolute, theUri) Then
                Return theUri.ToString()
            Else
                If Uri.TryCreate(New Uri("http://www.nbc.com"), url, theUri) Then
                    Return theUri.ToString()
                End If
            End If
            Return Nothing
        End Function

        Private Function GetLargestImageUrl(url As String) As String
            Return Regex.Replace(url, "\/styles\/.+\/public", String.Empty)
        End Function

        Private Function LocalEpisodeImagePath(tvEp As TvDataEpisode) As String
            Return IO.Path.Combine(SeasonDownloadFolder(tvEp.SeasonNumber),
                                   SeasonEpisodeString(tvEp.SeasonNumber, tvEp.EpisodeNumber) &
                                   "_" & tvEp.EpisodeName.MakeFileNameSafeNoSpaces() & ".jpg")
        End Function

        Private Function SeasonEpisodeString(seasonNo As Integer, episodeNumber As Integer) As String
            Return "S" & seasonNo.ToString("00") & "E" & episodeNumber.ToString("00")
        End Function

#End Region

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
                                   "Season " & seasonNo.ToString("00"))
        End Function

#End Region

#Region " Data Structures "

        <DataContract>
        Public Class NbcEpisodesPage

            <DataMember(Name:="data")>
            Public Property Data As List(Of Datum)

            <DataMember(Name:="included")>
            Public Property Included As List(Of Included)

            <DataMember(Name:="meta")>
            Public Property Meta As Meta

            <DataMember(Name:="links")>
            Public Property Links As Links

            Public Function GetEpisodeImageUrl(episodeImageId As String) As String
                Dim imageInfo = (From i In Included
                                 Where i.Id = episodeImageId).First()
                If imageInfo IsNot Nothing Then
                    Return imageInfo.Attributes.Derivatives.Landscape.Widescreen.Size350.X1.Uri
                Else
                    Return Nothing
                End If
            End Function

        End Class

        <DataContract>
        Public Class Datum

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="id")>
            Public Property Id As String

            <DataMember(Name:="attributes")>
            Public Property Attributes As Attributes

            <DataMember(Name:="relationships")>
            Public Property Relationships As Relationships

            <DataMember(Name:="links")>
            Public Property Links As Links
        End Class

        <DataContract>
        Public Class Included

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="id")>
            Public Property Id As String

            <DataMember(Name:="attributes")>
            Public Property Attributes As IncludedAttributes

            <DataMember(Name:="links")>
            Public Property Links As Links
        End Class

        <DataContract>
        Public Class Meta

            <DataMember(Name:="count")>
            Public Property Count As Integer

            <DataMember(Name:="version")>
            Public Property Version As String
        End Class

        <DataContract>
        Public Class Links

            <DataMember(Name:="related")>
            Public Property Related As String

            <DataMember(Name:="self")>
            Public Property Self As String

            <DataMember(Name:="next")>
            Public Property [Next] As String
        End Class

        <DataContract>
        Public Class Attributes

            <DataMember(Name:="airdate")>
            Public Property Airdate As String

            <DataMember(Name:="description")>
            Public Property Description As String

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="seasonNumber")>
            Public Property SeasonNumber As String

            <DataMember(Name:="episodeNumber")>
            Public Property EpisodeNumber As String
        End Class

        <DataContract>
        Public Class Relationships

            <DataMember(Name:="image")>
            Public Property Image As NbcImage
        End Class

        <DataContract>
        Public Class IncludedAttributes

            <DataMember(Name:="derivatives")>
            Public Property Derivatives As Derivatives
        End Class

        <DataContract>
        Public Class Derivatives

            <DataMember(Name:="landscape")>
            Public Property Landscape As Landscape
        End Class

        <DataContract>
        Public Class Landscape

            <DataMember(Name:="widescreen")>
            Public Property Widescreen As Widescreen
        End Class

        <DataContract>
        Public Class Widescreen

            <DataMember(Name:="size350")>
            Public Property Size350 As Size350
        End Class

        <DataContract>
        Public Class Size350

            <DataMember(Name:="x1")>
            Public Property X1 As X1
        End Class

        <DataContract>
        Public Class X1

            <DataMember(Name:="width")>
            Public Property Width As Integer

            <DataMember(Name:="height")>
            Public Property Height As Integer

            <DataMember(Name:="uri")>
            Public Property Uri As String
        End Class

        <DataContract>
        Public Class NbcImage

            <DataMember(Name:="data")>
            Public Property Data As RelationshipsData
        End Class

        <DataContract>
        Public Class RelationshipsData

            <DataMember(Name:="type")>
            Public Property Type As String

            <DataMember(Name:="id")>
            Public Property Id As String
        End Class

#End Region

    End Class

End Namespace
