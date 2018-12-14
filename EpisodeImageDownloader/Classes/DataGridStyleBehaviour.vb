Imports System.Collections.Specialized

Namespace Behaviors

    Public NotInheritable Class DataGridBehaviour
        Private Sub New()
        End Sub

#Region " Select All Button Style "

#Region " Attached Property "

        Public Shared Function GetSelectAllButtonTemplate(obj As DataGrid) As ControlTemplate
            Return DirectCast(obj.GetValue(SelectAllButtonTemplateProperty), ControlTemplate)
        End Function

        Public Shared Sub SetSelectAllButtonTemplate(obj As DataGrid, value As ControlTemplate)
            obj.SetValue(SelectAllButtonTemplateProperty, value)
        End Sub

        Public Shared ReadOnly SelectAllButtonTemplateProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("SelectAllButtonTemplate",
                                                GetType(ControlTemplate),
                                                GetType(DataGridBehaviour),
                                                New UIPropertyMetadata(Nothing, AddressOf OnSelectAllButtonTemplateChanged))

#End Region

#Region " Property Behaviour "

        ' property change event handler for SelectAllButtonTemplate
        Private Shared Sub OnSelectAllButtonTemplateChanged(depObj As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim grid As DataGrid = TryCast(depObj, DataGrid)
            If grid Is Nothing Then
                Return
            End If

            ' handle the grid's Loaded event
            AddHandler grid.Loaded, AddressOf Grid_Loaded
        End Sub

        ' Handles the DataGrid's Loaded event
        Private Shared Sub Grid_Loaded(sender As Object, e As RoutedEventArgs)
            Dim grid As DataGrid = TryCast(sender, DataGrid)
            Dim dep As DependencyObject = grid
            Try
                ' Navigate down the visual tree to the button
                While Not (TypeOf dep Is Button)
                    dep = VisualTreeHelper.GetChild(dep, 0)

                End While
                Dim button As Button = TryCast(dep, Button)

                ' apply our new template
                Dim template As ControlTemplate = GetSelectAllButtonTemplate(grid)
                button.Template = template
            Catch ex As Exception

            End Try
        End Sub

#End Region

#End Region


#Region " AutoScroll "

        Public Shared ReadOnly AutoscrollProperty As DependencyProperty =
                    DependencyProperty.RegisterAttached("Autoscroll",
                                                        GetType(Boolean),
                                                        GetType(DataGridBehaviour),
                                                        New PropertyMetadata(False, AddressOf AutoscrollChangedCallback))

        Private Shared ReadOnly handlersDict As New Dictionary(Of DataGrid, NotifyCollectionChangedEventHandler)()

        Private Shared Sub AutoscrollChangedCallback(dependencyObject As DependencyObject, args As DependencyPropertyChangedEventArgs)
            Dim dataGrid = TryCast(dependencyObject, DataGrid)
            If dataGrid Is Nothing Then
                Throw New InvalidOperationException("Dependency object is not DataGrid.")
            End If

            If CBool(args.NewValue) Then
                Subscribe(dataGrid)
                AddHandler dataGrid.Unloaded, AddressOf DataGridOnUnloaded
                AddHandler dataGrid.Loaded, AddressOf DataGridOnLoaded
            Else
                Unsubscribe(dataGrid)
                RemoveHandler dataGrid.Unloaded, AddressOf DataGridOnUnloaded
                RemoveHandler dataGrid.Loaded, AddressOf DataGridOnLoaded
            End If
        End Sub

        Private Shared Sub Subscribe(dataGrid As DataGrid)
            'Dim handler = New NotifyCollectionChangedEventHandler(Function(sender, eventArgs) ScrollToEnd(dataGrid))
            Dim handler = New NotifyCollectionChangedEventHandler(Sub(sender, eventArgs)
                                                                      ScrollToEnd(dataGrid)
                                                                  End Sub)
            Try
                handlersDict.Add(dataGrid, handler)
            Catch ex As Exception
            End Try
            AddHandler DirectCast(dataGrid.Items, INotifyCollectionChanged).CollectionChanged, handler
            ScrollToEnd(dataGrid)
        End Sub

        Private Shared Sub Unsubscribe(dataGrid As DataGrid)
            Dim handler As NotifyCollectionChangedEventHandler = Nothing
            handlersDict.TryGetValue(dataGrid, handler)
            If handler Is Nothing Then
                Return
            End If
            RemoveHandler DirectCast(dataGrid.Items, INotifyCollectionChanged).CollectionChanged, handler
            handlersDict.Remove(dataGrid)
        End Sub

        Private Shared Sub DataGridOnLoaded(sender As Object, routedEventArgs As RoutedEventArgs)
            Dim dataGrid = DirectCast(sender, DataGrid)
            If GetAutoscroll(dataGrid) Then
                Subscribe(dataGrid)
            End If
        End Sub

        Private Shared Sub DataGridOnUnloaded(sender As Object, routedEventArgs As RoutedEventArgs)
            Dim dataGrid = DirectCast(sender, DataGrid)
            If GetAutoscroll(dataGrid) Then
                Unsubscribe(dataGrid)
            End If
        End Sub

        Private Shared Sub ScrollToEnd(datagrid As DataGrid)
            If datagrid.Items.Count = 0 Then
                Return
            End If
            datagrid.ScrollIntoView(datagrid.Items(datagrid.Items.Count - 1))
        End Sub

        Public Shared Sub SetAutoscroll(element As DependencyObject, value As Boolean)
            element.SetValue(AutoscrollProperty, value)
        End Sub

        Public Shared Function GetAutoscroll(element As DependencyObject) As Boolean
            Return CBool(element.GetValue(AutoscrollProperty))
        End Function

#End Region

    End Class

End Namespace