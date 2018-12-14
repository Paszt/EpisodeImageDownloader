Imports System.ComponentModel

Namespace ViewModels

    Public Class SeasonNumberUrlInfo
        Implements INotifyDataErrorInfo

        Private _number As Integer?
        Public Property Number As Integer?
            Get
                Return _number
            End Get
            Set(value As Integer?)
                _number = value
                Validate("number")
            End Set
        End Property

        Private _url As String
        Public Property Url As String
            Get
                Return _url
            End Get
            Set(value As String)
                _url = value
                Validate("Url")
            End Set
        End Property

#Region " INotifyDataErrorInfo Members "

        Public Event ErrorsChanged As EventHandler(Of DataErrorsChangedEventArgs) Implements INotifyDataErrorInfo.ErrorsChanged
        Private _errors As New Dictionary(Of String, List(Of String))()

        Public ReadOnly Property HasErrors As Boolean Implements INotifyDataErrorInfo.HasErrors
            Get
                Try
                    Return (_errors.Where(Function(c) c.Value.Count > 0).Count() > 0)
                Catch ex As Exception
                    'swallow
                End Try
                Return Nothing
            End Get
        End Property

        Public Function GetErrors(propertyName As String) As IEnumerable Implements INotifyDataErrorInfo.GetErrors
            If String.IsNullOrEmpty(propertyName) Then
                Return (_errors.Values)
            End If

            EnsurePropertyErrorList(propertyName)
            Return _errors(propertyName)
        End Function

        Private Sub NotifyErrorsChanged(propertyName As String)
            RaiseEvent ErrorsChanged(Me, New ComponentModel.DataErrorsChangedEventArgs(propertyName))
        End Sub

        Public Sub ClearError(propertyName As String)
            EnsurePropertyErrorList(propertyName)
            _errors(propertyName).Clear()
            NotifyErrorsChanged(propertyName)
        End Sub

        Public Sub AddError(propertyName As String, [error] As String)
            EnsurePropertyErrorList(propertyName)
            _errors(propertyName).Add([error])
            NotifyErrorsChanged(propertyName)
        End Sub

        Private Sub EnsurePropertyErrorList(propertyName As String)
            If Not _errors.ContainsKey(propertyName) Then
                _errors(propertyName) = New List(Of String)()
            End If
        End Sub

        ''' <summary>
        ''' Force the object to validate itself using the assigned business rules.
        ''' </summary>
        ''' <param name="propertyName">Name of the property to validate.</param>
        Private Sub Validate(propertyName As String)
            If String.IsNullOrEmpty(propertyName) Then
                Return
            End If

            Select Case propertyName
                Case "Url"
                    Dim success As Boolean = False
                    Try
                        Dim uri = New Uri(Url)
                        success = True
                    Catch ex As Exception
                        AddError(propertyName, ex.Message)
                    End Try
                    If success Then
                        ClearError(propertyName)
                    End If
            End Select
        End Sub

#End Region

    End Class

End Namespace
