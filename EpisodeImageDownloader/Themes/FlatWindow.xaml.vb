Option Strict On
Imports System.Windows.Interop
Imports System.Runtime.InteropServices
Imports EpisodeImageDownloader.WinApi
Imports System.ComponentModel

<TemplatePart(Name:="PART_TitleBar", Type:=GetType(UIElement))>
<TemplatePart(Name:="PART_WindowTitleBackground", Type:=GetType(UIElement))>
<TemplatePart(Name:="PART_Max", Type:=GetType(Button))>
<TemplatePart(Name:="PART_Close", Type:=GetType(Button))>
<TemplatePart(Name:="PART_Min", Type:=GetType(Button))>
<TemplatePart(Name:="PART_Icon", Type:=GetType(Border))>
Public Class FlatWindow
    Inherits Window

    Private Const PART_TitleBar As String = "PART_TitleBar"
    Private Const PART_WindowTitleBackground As String = "PART_WindowTitleBackground"
    Private Const PART_Icon As String = "PART_Icon"
    Private Const PART_Max As String = "PART_Max"
    Private Const PART_Close As String = "PART_Close"
    Private Const PART_Min As String = "PART_Min"
    Private Const PART_BorderOutline As String = "PART_BorderOutline"

    Private titleBar As UIElement
    Private titleBarBackground As UIElement
    Private titleBaricon As Border
    Private minButton As Button
    Private maxButton As Button
    Private closeButton As Button
    Private resizeGrid As Grid
    Private windowBorder As Border

    Shared Sub New()
        DefaultStyleKeyProperty.OverrideMetadata(GetType(FlatWindow), New FrameworkPropertyMetadata(GetType(FlatWindow)))
    End Sub

#Region " Dependency Properties "

    Public Shared ReadOnly TitlebarHeightProperty As DependencyProperty = DependencyProperty.Register("TitlebarHeight", GetType(Integer),
                                                                                                         GetType(FlatWindow), New PropertyMetadata(34))

    Public Property TitlebarHeight() As Integer
        Get
            Return CInt(GetValue(TitlebarHeightProperty))
        End Get
        Set(value As Integer)
            SetValue(TitlebarHeightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly BorderColorProperty As DependencyProperty = DependencyProperty.Register("BorderColor", GetType(SolidColorBrush),
                                                                                                   GetType(FlatWindow), New PropertyMetadata(CType(Application.Current.Resources("BackgroundSelected"), SolidColorBrush)))
    <Category("Common")>
    Public Property BorderColor() As SolidColorBrush
        Get
            Return CType(GetValue(BorderColorProperty), SolidColorBrush)
        End Get
        Set(value As SolidColorBrush)
            SetValue(BorderColorProperty, value)
        End Set
    End Property

    Public Shared ReadOnly StatusBarTextProperty As DependencyProperty = DependencyProperty.Register("StatusBarText", GetType(String),
                                                                                             GetType(FlatWindow), New PropertyMetadata("Ready"))

    Public Property StatusBarText As String
        Get
            Return CStr(GetValue(StatusBarTextProperty))
        End Get
        Set(value As String)
            SetValue(StatusBarTextProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ShowStatusBarProperty As DependencyProperty = DependencyProperty.Register("ShowStatusBar", GetType(Boolean),
                                                                                             GetType(FlatWindow), New PropertyMetadata(True))

    <Category("Common")>
    Public Property ShowStatusBar As Boolean
        Get
            Return CBool(GetValue(ShowStatusBarProperty))
        End Get
        Set(value As Boolean)
            SetValue(ShowStatusBarProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ShowMinMaxProperty As DependencyProperty = DependencyProperty.Register("ShowMinMax", GetType(Boolean),
                                                                                                 GetType(FlatWindow), New PropertyMetadata(True))

    <Category("Common")>
    Public Property ShowMinMax As Boolean
        Get
            Return CBool(GetValue(ShowMinMaxProperty))
        End Get
        Set(value As Boolean)
            SetValue(ShowMinMaxProperty, value)
        End Set
    End Property

#End Region

    Protected Sub TitleBarMouseDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left Then
            ' if UseNoneWindowStyle = true no movement, no maximize please
            Dim windowHandle As IntPtr = New WindowInteropHelper(Me).Handle
            UnsafeNativeMethods.ReleaseCapture()

            Dim mPoint = Mouse.GetPosition(Me)

            Dim wpfPoint = PointToScreen(mPoint)
            Dim x = Convert.ToInt16(wpfPoint.X)
            Dim y = Convert.ToInt16(wpfPoint.Y)
            Dim lParam = x Or (y << 16)
            UnsafeNativeMethods.SendMessage(windowHandle, Constants.WMNCLBUTTONDOWN, CType(Constants.HTCAPTION, IntPtr), CType(lParam, IntPtr))

            If e.ClickCount = 2 AndAlso (Me.ResizeMode = ResizeMode.CanResizeWithGrip OrElse Me.ResizeMode = ResizeMode.CanResize) AndAlso mPoint.Y <= TitlebarHeight AndAlso TitlebarHeight > 0 Then
                If WindowState = WindowState.Maximized Then
                    WindowState = WindowState.Normal
                Else
                    WindowState = WindowState.Maximized
                End If
            End If
        End If
    End Sub

    Public Overrides Sub OnApplyTemplate()
        MyBase.OnApplyTemplate()
        titleBar = CType(GetTemplateChild(PART_TitleBar), UIElement)
        AddHandler titleBar.MouseDown, AddressOf TitleBarMouseDown
        titleBarBackground = CType(GetTemplateChild(PART_WindowTitleBackground), UIElement)
        AddHandler titleBarBackground.MouseDown, AddressOf TitleBarMouseDown
        titleBaricon = CType(GetTemplateChild(PART_Icon), Border)
        AddHandler titleBaricon.MouseDown, AddressOf TitleBarMouseDown

        closeButton = TryCast(Template.FindName("PART_Close", Me), Button)
        If closeButton IsNot Nothing Then
            AddHandler closeButton.Click, AddressOf CloseClick
        End If

        maxButton = TryCast(Template.FindName("PART_Max", Me), Button)
        If maxButton IsNot Nothing Then
            AddHandler maxButton.Click, AddressOf MaximiseClick
        End If

        minButton = TryCast(Template.FindName("PART_Min", Me), Button)
        If minButton IsNot Nothing Then
            AddHandler minButton.Click, AddressOf MinimiseClick
        End If

        windowBorder = TryCast(Template.FindName(PART_BorderOutline, Me), Border)

        resizeGrid = TryCast(GetTemplateChild("resizeGrid"), Grid)
        If resizeGrid IsNot Nothing Then
            For Each element As UIElement In resizeGrid.Children
                Dim resizeRectangle As Rectangle = TryCast(element, Rectangle)
                If resizeRectangle IsNot Nothing Then
                    AddHandler resizeRectangle.PreviewMouseLeftButtonDown, AddressOf Resize
                    'AddHandler resizeRectangle.MouseMove, AddressOf resizeRectangle_MouseMove
                End If
            Next
        End If
        RefreshMaximiseIconState()
    End Sub


#Region " WindowProc "

    Private Function WindowProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        Select Case msg
            Case Constants.WMGETMINMAXINFO
                Dim mmi = CType(Marshal.PtrToStructure(lParam, GetType(MINMAXINFO)), MINMAXINFO)
                Dim nearestScreen = Screen

                mmi.ptMaxPosition.x = 0
                mmi.ptMaxPosition.y = 0
                mmi.ptMaxSize.x = nearestScreen.WorkingArea.Right - nearestScreen.WorkingArea.Left
                mmi.ptMaxSize.y = nearestScreen.WorkingArea.Bottom - nearestScreen.WorkingArea.Top
                mmi.ptMinTrackSize.x = CInt(MinWidth)
                mmi.ptMinTrackSize.y = CInt(MinHeight)

                Marshal.StructureToPtr(mmi, lParam, True)
                handled = True
                'Case Constants.WM_GETMINMAXINFO
                '    Dim mmi = CType(Marshal.PtrToStructure(lParam, GetType(MINMAXINFO)), MINMAXINFO)
                '    mmi.ptMaxPosition.x = Screen.WorkingArea.Left - 1
                '    mmi.ptMaxPosition.y = Screen.WorkingArea.Top - 1
                '    mmi.ptMaxSize.x = Screen.WorkingArea.Width + 2
                '    mmi.ptMaxSize.y = Screen.WorkingArea.Height + 2
                '    Marshal.StructureToPtr(mmi, lParam, True)
                '    handled = True
                'Case Constants.WM_WINDOWPOSCHANGING
                '    Dim pos As WINDOWPOS = CType(Marshal.PtrToStructure(lParam, GetType(WINDOWPOS)), WINDOWPOS)
                '    If (pos.flags And CInt(SetWindowPosFlags.NOMOVE)) <> 0 Then
                '        Return IntPtr.Zero
                '    End If
                '    Dim wnd As Window = DirectCast(HwndSource.FromHwnd(hwnd).RootVisual, Window)
                '    If wnd Is Nothing Then
                '        Return IntPtr.Zero
                '    End If
                '    Dim changedPos As Boolean = False
                '    If pos.cx < Me.MinWidth Then
                '        pos.cx = CInt(Me.MinWidth)
                '        changedPos = True
                '    End If
                '    If pos.cy < Me.MinHeight Then
                '        pos.cy = CInt(Me.MinHeight)
                '        changedPos = True
                '    End If
                '    If Not changedPos Then
                '        Return IntPtr.Zero
                '    End If
                '    pos.flags = CInt(pos.flags + Constants.SWP_NOMOVE)

                '    Marshal.StructureToPtr(pos, lParam, True)
                '    handled = True

        End Select
        Return IntPtr.Zero
    End Function

    Private Sub FlatWindow_SourceInitialized(sender As Object, e As EventArgs) Handles Me.SourceInitialized
        Dim window = TryCast(sender, Window)

        If window IsNot Nothing Then
            Dim handle As IntPtr = New WindowInteropHelper(window).Handle
            HwndSource.FromHwnd(handle).AddHook(AddressOf WindowProc)
        End If
    End Sub

    Private ReadOnly Property Screen As Forms.Screen
        Get
            Return Forms.Screen.FromHandle(New WindowInteropHelper(Me).Handle)
        End Get
    End Property

#End Region

#Region " Window Button Event Handlers "

    Public Event ClosingWindow As ClosingWindowEventHandler
    Public Delegate Sub ClosingWindowEventHandler(sender As Object, args As ClosingWindowEventHandlerArgs)

    Private Sub CloseClick(sender As Object, e As RoutedEventArgs)
        Dim closingWindowEventHandlerArgs = New ClosingWindowEventHandlerArgs()
        OnClosingWindow(closingWindowEventHandlerArgs)

        If closingWindowEventHandlerArgs.Cancelled Then
            Return
        End If

        Close()
    End Sub

    Protected Sub OnClosingWindow(args As ClosingWindowEventHandlerArgs)
        RaiseEvent ClosingWindow(Me, args)
    End Sub

    Private Sub MaximiseClick(sender As Object, e As RoutedEventArgs)
        If WindowState = WindowState.Maximized Then
            WindowState = Windows.WindowState.Normal
        Else
            WindowState = Windows.WindowState.Maximized
        End If

        RefreshMaximiseIconState()
    End Sub

    Private Sub MinimiseClick(sender As Object, e As RoutedEventArgs)
        WindowState = WindowState.Minimized
    End Sub

    Private Sub RefreshMaximiseIconState()
        Dim maxpath = DirectCast(maxButton.FindName("MaximisePath"), Path)
        Dim restorepath = DirectCast(maxButton.FindName("RestorePath"), Path)
        If Me.WindowState = WindowState.Normal Then
            maxpath.Visibility = Visibility.Visible
            restorepath.Visibility = Visibility.Collapsed
            maxButton.ToolTip = "Maximize"
            resizeGrid.Visibility = Windows.Visibility.Visible
            windowBorder.Margin = New Thickness(5)
        Else
            restorepath.Visibility = Visibility.Visible
            maxpath.Visibility = Visibility.Collapsed
            resizeGrid.Visibility = Windows.Visibility.Hidden
            maxButton.ToolTip = "Restore"
            windowBorder.Margin = New Thickness(0)
        End If
    End Sub

    Protected Overrides Sub OnStateChanged(e As EventArgs)
        RefreshMaximiseIconState()
        MyBase.OnStateChanged(e)
    End Sub

#End Region

#Region " Window Resize "

    Private Sub Resize(sender As Object, e As MouseButtonEventArgs)
        Dim resizeRect As Rectangle = TryCast(sender, Rectangle)
        Cursor = resizeRect.Cursor
        If WindowState <> WindowState.Maximized Then
            UnsafeNativeMethods.SendMessage(New WindowInteropHelper(Me).Handle, Constants.WMSYSCOMMAND, CType(61440 + CInt(CType(sender, Rectangle).Tag), IntPtr), IntPtr.Zero)
        End If
    End Sub

    Private Sub ResizeRectangle_MouseMove(sender As Object, e As MouseEventArgs)
        Dim ResizeRect As Rectangle = TryCast(sender, Rectangle)
        Cursor = ResizeRect.Cursor
    End Sub

    Private Sub FlatWindow_PreviewMouseMove(sender As Object, e As MouseEventArgs) Handles Me.PreviewMouseMove
        If Mouse.LeftButton = MouseButtonState.Released Then
            Cursor = Nothing
        End If
    End Sub

    Private Sub FlatWindow_StateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        Dim border As Border = CType(GetTemplateChild("PART_BorderOutline"), Border)
        If WindowState = WindowState.Maximized Then
            border.BorderBrush = CType(Windows.Application.Current.FindResource("Background"), Brush)
        Else
            border.BorderBrush = BorderColor
        End If
    End Sub

#End Region

End Class

Public Class ClosingWindowEventHandlerArgs
    Inherits EventArgs

    Public Property Cancelled() As Boolean

End Class