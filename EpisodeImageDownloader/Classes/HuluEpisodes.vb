Imports System.Runtime.Serialization

<DataContract>
Public Class HuluVideo

    <DataMember(Name:="id")>
    Public Property Id As Integer

    <DataMember(Name:="content_id")>
    Public Property ContentId As String

    <DataMember(Name:="description")>
    Public Property Description As String

    <DataMember(Name:="duration")>
    Public Property Duration As String

    <DataMember(Name:="eid")>
    Public Property Eid As String

    <DataMember(Name:="episode_number")>
    Public Property EpisodeNumber As Integer

    <DataMember(Name:="original_premiere_date")>
    Private Property OriginalPremiereDate As String

    <IgnoreDataMember>
    Public ReadOnly Property FirstAired As String
        Get
            Dim dte As Date
            Dim culture = Globalization.CultureInfo.InvariantCulture
            Dim styles = Globalization.DateTimeStyles.AdjustToUniversal
            If DateTime.TryParse(OriginalPremiereDate, culture, styles, dte) Then
                Return dte.ToString("yyyy-MM-dd")
            Else
                Return OriginalPremiereDate
            End If
        End Get
    End Property

    <DataMember(Name:="programming_type")>
    Public Property ProgrammingType As String

    <DataMember(Name:="rating")>
    Public Property Rating As Double

    <DataMember(Name:="season_number")>
    Public Property SeasonNumber As Integer

    <DataMember(Name:="show_id")>
    Public Property ShowId As Integer

    <DataMember(Name:="title")>
    Public Property Title As String

    <DataMember(Name:="thumbnail_url")>
    Public Property ThumbnailUrl As String

    <DataMember(Name:="type")>
    Public Property Type As String

    <DataMember(Name:="video_type")>
    Public Property VideoType As String

    Public ReadOnly Property ImageUrl(Optional Size As String = "640x360") As String
        Get
            Dim format = "http://ib2.huluim.com/video/{0}?size={1}&img=1"
            Return String.Format(format, ContentId, Size)
        End Get
    End Property

End Class
<DataContract>
Public Class HuluEpisodes

    <DataMember(Name:="data")>
    Public Property Data As List(Of HuluDatum)

    <DataMember(Name:="total_count")>
    Public Property TotalCount As Integer

    <DataMember(Name:="position")>
    Public Property Position As Integer

    <DataMember(Name:="meta_data")>
    Public Property MetaData As HuluMetaData

    <DataContract>
    Public Class HuluDatum

        <DataMember(Name:="video")>
        Public Property Video As HuluVideo

    End Class

    <DataContract>
    Public Class HuluMetaData

        <DataMember(Name:="cache_time")>
        Public Property CacheTime As String

        <DataMember(Name:="generation_time")>
        Public Property GenerationTime As String

    End Class

End Class