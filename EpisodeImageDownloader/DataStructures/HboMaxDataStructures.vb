
Imports System.Runtime.Serialization


Namespace DataStructures

    <DataContract>
    Public Class HboMaxDatum

        <DataMember(Name:="id")>
        Public Property Id As String

        <DataMember(Name:="body")>
        Public Property Body As HboMaxDatumBody
    End Class

    <DataContract>
    Public Class HboMaxDatumBody

        <DataMember(Name:="duration")>
        Public Property Duration As Integer

        <DataMember(Name:="titles")>
        Public Property Titles As HboMaxTexts

        <DataMember(Name:="summaries")>
        Public Property Summaries As HboMaxTexts

        <DataMember(Name:="images")>
        Public Property Images As HboMaxImages

        <DataMember(Name:="firstOfferedDate")>
        Public Property FirstOfferedDate As String

        <DataMember(Name:="numberInSeason")>
        Public Property NumberInSeason As Integer?

        <DataMember(Name:="numberInSeries")>
        Public Property NumberInSeries As Integer?

        <DataMember(Name:="seasonNumber")>
        Public Property SeasonNumber As Integer?

        <DataMember(Name:="shouldUseEpisodePrefix")>
        Public Property ShouldUseEpisodePrefix As Boolean?

        <DataMember(Name:="extraType")>
        Public Property ExtraType As String
    End Class

    <DataContract>
    Public Class HboMaxTexts

        <DataMember(Name:="short")>
        <CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification:="<Pending>")>
        Public Property [Short] As String

        <DataMember(Name:="full")>
        Public Property Full As String

        <DataMember(Name:="ultraShort")>
        Public Property UltraShort As String
    End Class

    <DataContract>
    Public Class HboMaxImages

        <DataMember(Name:="tile")>
        Public Property Tile As String

        <DataMember(Name:="tilezoom")>
        Public Property Tilezoom As String

        <DataMember(Name:="tileburnedin")>
        Public Property Tileburnedin As String

        <DataMember(Name:="background")>
        Public Property Background As String

        <DataMember(Name:="backgroundburnedin")>
        Public Property Backgroundburnedin As String

        <DataMember(Name:="logo")>
        Public Property Logo As String
    End Class

End Namespace
