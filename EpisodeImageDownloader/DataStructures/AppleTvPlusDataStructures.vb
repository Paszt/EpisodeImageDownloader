Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions

Namespace DataStructures

    <DataContract>
    Public Class AppleTvShowMainInfo

        <DataMember(Name:="data")>
        Public Property Data As AppleTvShowMainData

    End Class

    <DataContract>
    Public Class AppleTvShowMainData

        <DataMember(Name:="content")>
        Public Property Content As AppleTvPlusContent

        <DataMember(Name:="roles")>
        Public Property Roles As List(Of AppleTvPlusRole)

    End Class

    <DataContract>
    Public Class AppleTvPlusContent

        <DataMember(Name:="title")>
        Public Property Title As String

        <DataMember(Name:="description")>
        Public Property Description As String

        <DataMember(Name:="images")>
        Public Property Images As AppleTvPlusImages

    End Class

    <DataContract>
    Public Class AppleTvPlusRole

        <DataMember(Name:="type")>
        Public Property Type As String

        <DataMember(Name:="roleTitle")>
        Public Property RoleTitle As String

        <DataMember(Name:="characterName")>
        Public Property CharacterName As String

        <DataMember(Name:="images")>
        Public Property Images As AppleTvPlusImages

        <DataMember(Name:="personName")>
        Public Property PersonName As String

        <DataMember(Name:="personId")>
        Public Property PersonId As String

        <DataMember(Name:="url")>
        <CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings",
                                      Justification:="Serialization requires property to be String")>
        Public Property Url As String
    End Class

    <DataContract>
    Public Class AppleTvPlusShowData

        <DataMember(Name:="data")>
        Public Property Data As AppleTvPlusData

    End Class

    <DataContract>
    Public Class AppleTvPlusData

        <DataMember(Name:="showId")>
        Public Property ShowId As String

        <DataMember(Name:="episodes")>
        Public Property Episodes As List(Of AppleTvPlusEpisode)

        <DataMember(Name:="version")>
        Public Property Version As String

        <DataMember(Name:="seasonSummaries")>
        Public Property SeasonSummaries As List(Of AppleTvPlusSeasonSummary)

        <DataMember(Name:="totalEpisodeCount")>
        Public Property TotalEpisodeCount As Integer

        <DataMember(Name:="seasons")>
        Public Property Seasons As List(Of AppleTvPlusSeason)

    End Class

    <DataContract>
    Public Class AppleTvPlusSeasonSummary

        <DataMember(Name:="label")>
        Public Property Label As String

        <DataMember(Name:="episodeCount")>
        Public Property EpisodeCount As Integer

    End Class

    <DataContract>
    Public Class AppleTvPlusSeason

        <DataMember(Name:="id")>
        Public Property Id As String

        <DataMember(Name:="type")>
        Public Property Type As String

        <DataMember(Name:="title")>
        Public Property Title As String

        <DataMember(Name:="images")>
        Public Property Images As AppleTvPlusImages

        <DataMember(Name:="url")>
        <CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings",
                                      Justification:="Serialization requires property to be String")>
        Public Property Url As String

        <DataMember(Name:="seasonNumber")>
        Public Property SeasonNumber As Integer

        <DataMember(Name:="showId")>
        Public Property ShowId As String

        <DataMember(Name:="showTitle")>
        Public Property ShowTitle As String

    End Class

    <DataContract>
    Public Class AppleTvPlusEpisode

        <DataMember(Name:="id")>
        Public Property Id As String

        <DataMember(Name:="type")>
        Public Property Type As String

        <DataMember(Name:="title")>
        Public Property Title As String

        <DataMember(Name:="description")>
        Public Property Description As String

        <DataMember(Name:="releaseDate")>
        Public Property ReleaseDate As Long?

        <DataMember(Name:="images")>
        Public Property Images As AppleTvPlusImages

        <DataMember(Name:="showTitle")>
        Public Property ShowTitle As String

        <DataMember(Name:="showImages")>
        Public Property ShowImages As AppleTvPlusImages

        <DataMember(Name:="seasonImages")>
        Public Property SeasonImages As AppleTvPlusImages

        <DataMember(Name:="episodeNumber")>
        Public Property EpisodeNumber As Integer

        <DataMember(Name:="seasonNumber")>
        Public Property SeasonNumber As Integer

        <DataMember(Name:="episodeIndex")>
        Public Property EpisodeIndex As Integer
    End Class

    <DataContract>
    Public Class AppleTvPlusImages

        <DataMember(Name:="previewFrame")>
        Public Property PreviewFrame As AppleTvPlusImageInformation

        <DataMember(Name:="coverArt16X9")>
        Public Property CoverArt16X9 As AppleTvPlusImageInformation

        <DataMember(Name:="coverArt")>
        Public Property CoverArt As AppleTvPlusImageInformation

        <DataMember(Name:="singleColorContentLogo")>
        Public Property SingleColorContentLogo As AppleTvPlusImageInformation

        <DataMember(Name:="fullColorContentLogo")>
        Public Property FullColorContentLogo As AppleTvPlusImageInformation

        <DataMember(Name:="centeredFullScreenBackgroundImage")>
        Public Property CenteredFullScreenBackgroundImage As AppleTvPlusImageInformation

        <DataMember(Name:="centeredFullScreenBackgroundSmallImage")>
        Public Property CenteredFullScreenBackgroundSmallImage As AppleTvPlusImageInformation

        <DataMember(Name:="fullScreenBackground")>
        Public Property FullScreenBackground As AppleTvPlusImageInformation

        <DataMember(Name:="bannerUberImage")>
        Public Property BannerUberImage As AppleTvPlusImageInformation

        <DataMember(Name:="contentLogo")>
        Public Property ContentLogo As AppleTvPlusImageInformation

        <DataMember(Name:="castInCharacter")>
        Public Property CastInCharacter As AppleTvPlusImageInformation

    End Class

    <DataContract>
    Public Class AppleTvPlusImageInformation

        <DataMember(Name:="width")>
        Public Property Width As Integer

        <DataMember(Name:="height")>
        Public Property Height As Integer

        <DataMember(Name:="url")>
        <CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings",
                                      Justification:="Serialization requires property to be String")>
        Public Property Url As String

        <DataMember(Name:="supportsLayeredImage")>
        Public Property SupportsLayeredImage As Boolean

        Function GetMaxUrl(Optional imageExtension As String = "jpg") As Uri
            Return New Uri(Regex.Replace(Url, "\{w\}x\{h\}(\{c\})?(\w+)?\.\{f\}", "9999x9999." & imageExtension))
        End Function

        Function GetImageUrl(size As String, Optional imageExtension As String = "jpg") As Uri
            If size = "Max" Then Return GetMaxUrl()
            Return New Uri(Regex.Replace(Url, "\{w\}x\{h\}(\{c\})?(\w+)?\.\{f\}", size & "." & imageExtension))
        End Function

        Function GetLsrUrl() As Uri
            Return New Uri(Regex.Replace(Url, "\{w\}x\{h\}(\{c\})?(\w+)?\.\{f\}", Width & "x" & Height & ".lsr"))
        End Function

    End Class

End Namespace
