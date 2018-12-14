Imports System.Runtime.Serialization

<DataContract>
Public Class CrunchyrollEpisode

    <IgnoreDataMember>
    Public Property SeasonNumber As Integer

    <IgnoreDataMember>
    Public Property EpisodeNumber As Integer

    <DataMember(Name:="name")>
    Public Property Name As String

    <DataMember(Name:="description")>
    Public Property Overview As String

End Class
