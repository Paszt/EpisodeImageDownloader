Imports System.Runtime.Serialization

<DataContract>
Public Class DiscoveryEpisode

    <DataMember(Name:="description")>
    Public Property Description As DiscoveryEpisodeDescription

    <DataMember(Name:="episodeNumber")>
    Public Property EpisodeNumber As Integer

    <DataMember(Name:="image")>
    Public Property Image As DiscoveryEpisodeImage

    <DataMember(Name:="name")>
    Public Property Name As String

    <DataMember(Name:="networks")>
    Private Property Networks As List(Of Network)

    <DataMember(Name:="slug")>
    Public Property Slug As String

    <DataMember(Name:="socialUrl")>
    Public Property SocialUrl As String

    <DataMember(Name:="alternateImages")>
    Public Property AlternateImages As Object()

    <DataMember(Name:="type")>
    Public Property Type As String

    <DataMember(Name:="id")>
    Public Property Id As String

    <DataMember(Name:="duration")>
    Public Property Duration As Integer

    <DataMember(Name:="season")>
    Public Property Season As DiscoverySeason

    <IgnoreDataMember>
    Public ReadOnly Property FirstAired As String
        Get
            Dim netwk = Networks.Where(Function(n) n.Primary = True).First()
            If netwk IsNot Nothing Then
                Dim dte As Date
                If Date.TryParse(netwk.AirDate, dte) Then
                    Return dte.ToString("yyyy-MM-dd")
                End If
                Return String.Empty
            End If
            Return String.Empty
        End Get
    End Property

    <DataContract>
    Public Class DiscoveryEpisodeDescription

        <DataMember(Name:="standard")>
        Public Property Standard As String

        <DataMember(Name:="detailed")>
        Public Property Detailed As String

    End Class

    <DataContract>
    Public Class DiscoveryEpisodeImage

        <DataMember(Name:="links")>
        Public Property Links As List(Of Link)

    End Class

    <DataContract>
    Public Class Link

        <DataMember(Name:="rel")>
        Public Property Rel As String

        <DataMember(Name:="href")>
        Public Property Href As String

    End Class

    <DataContract>
    Public Class Network

        <DataMember(Name:="airDate")>
        Public Property AirDate As String

        <DataMember(Name:="primary")>
        Public Property Primary As Boolean

    End Class

    <DataContract>
    Public Class DiscoverySeason

        <DataMember(Name:="name")>
        Public Property Name As String

        <DataMember(Name:="number")>
        Public Property Number As Integer

        <DataMember(Name:="id")>
        Public Property Id As String

    End Class

End Class
