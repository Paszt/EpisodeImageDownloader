Imports System.IO
Imports System.Text
Imports System.Runtime.Serialization.Json

Public Module JsonExtensions

    ''' <summary>
    ''' Creates a list based on a JSON Array
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="jsonArray"></param>
    ''' <returns></returns>
    <System.Runtime.CompilerServices.Extension> _
    Public Function FromJSONArray(Of T)(jsonArray As String) As IEnumerable(Of T)
        If String.IsNullOrEmpty(jsonArray) Then
            Return New List(Of T)()
        End If

        Try
            Using ms = New MemoryStream(Encoding.UTF8.GetBytes(jsonArray))
                Dim ser = New DataContractJsonSerializer(GetType(IEnumerable(Of T)))
                Dim result As IEnumerable(Of T) = Nothing
                Try
                    result = DirectCast(ser.ReadObject(ms), IEnumerable(Of T))
                Catch ex As Exception
                    MessageBox.Show(ex.Message, "While parsing JSON")
                End Try

                If result Is Nothing Then
                    Return New List(Of T)()
                Else
                    Return result
                End If
            End Using
        Catch generatedExceptionName As Exception
            Return New List(Of T)()
        End Try
    End Function

    ''' <summary>
    ''' Creates an object from JSON
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="json"></param>
    ''' <returns></returns>
    <System.Runtime.CompilerServices.Extension> _
    Public Function FromJSON(Of T)(json As String) As T
        If String.IsNullOrEmpty(json) Then
            Return Nothing
        End If

        Try
            Using ms = New MemoryStream(Encoding.UTF8.GetBytes(json.ToCharArray()))
                Dim ser = New DataContractJsonSerializer(GetType(T))
                Return DirectCast(ser.ReadObject(ms), T)
            End Using
        Catch generatedExceptionName As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Turns an object into JSON
    ''' </summary>
    ''' <param name="obj"></param>
    ''' <returns></returns>
    <System.Runtime.CompilerServices.Extension> _
    Public Function ToJSON(obj As Object) As String
        If obj Is Nothing Then
            Return String.Empty
        End If
        Using ms = New MemoryStream()
            Dim ser = New DataContractJsonSerializer(obj.[GetType]())
            ser.WriteObject(ms, obj)
            Return Encoding.UTF8.GetString(ms.ToArray())
        End Using
    End Function

End Module