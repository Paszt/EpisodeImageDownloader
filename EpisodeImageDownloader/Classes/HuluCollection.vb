Imports System.Runtime.Serialization

<DataContract>
Public Class HuluCollection

    <DataMember(Name:="items")>
    Public Property Items As List(Of HuluCollectionItem)

End Class

<DataContract>
Public Class HuluCollectionItem

    <DataMember(Name:="series_name")>
    Public Property SeriesName As String

    <DataMember(Name:="season")>
    Public Property Season As String

    <IgnoreDataMember>
    Public ReadOnly Property SeasonNumber As Integer?
        Get
            Dim returnValue As Integer
            If Integer.TryParse(Season, returnValue) Then
                Return returnValue
            End If
            Return Nothing
        End Get
    End Property

    <DataMember(Name:="number")>
    Public Property Number As String

    <IgnoreDataMember>
    Public ReadOnly Property EpisodeNumber As Integer?
        Get
            Dim returnValue As Integer
            If Integer.TryParse(Number, returnValue) Then
                Return returnValue
            End If
            Return Nothing
        End Get
    End Property

    <DataMember(Name:="premiere_date")>
    Public Property PremiereDate As String

    <DataMember(Name:="name")>
    Public Property Name As String

    <DataMember(Name:="description")>
    Public Property Description As String

    <DataMember(Name:="artwork")>
    Public Property Artwork As HuluArtwork

End Class

Public Class HuluArtwork

    <DataMember(Name:="video.horizontal.hero")>
    Public Property VideoHorizontalHero As HuluArtworkDetails
End Class

Public Class HuluArtworkDetails

    <DataMember(Name:="path")>
    Public Property Path As String

    <DataMember(Name:="image_type")>
    Public Property ImageType As String

    <DataMember(Name:="ratio")>
    Public Property Ratio As String

    <DataMember(Name:="width")>
    Public Property Width As Integer

    <DataMember(Name:="height")>
    Public Property Height As Integer

End Class