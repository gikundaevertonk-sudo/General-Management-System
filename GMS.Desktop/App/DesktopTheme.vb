Imports System.Drawing
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

Namespace App

    Public Enum ThemeMode
        Light
        Dark
    End Enum

    ''' <summary>
    ''' Light/dark theming for the Windows Forms client.
    ''' </summary>
    ''' <remarks>
    ''' Windows Forms has no equivalent of a stylesheet, and the colours here are spread across
    ''' a dozen designer files. Rather than edit every one of them, this walks a form's control
    ''' tree at run time and re-maps the colours it finds. That works because the whole client
    ''' only uses about a dozen distinct colours, all listed in the tables below.
    '''
    ''' Each control's colours are captured the first time it is themed, so switching back to
    ''' light restores exactly what the designer specified rather than an approximation. Because
    ''' of that, <see cref="Attach"/> must run before anything else recolours a control.
    '''
    ''' Background and foreground are mapped separately and deliberately: the same light colour
    ''' can mean two different things depending on where it is used. #1F2937 is the sidebar
    ''' hover background but also the text colour of a secondary button, and white is both a
    ''' card background and the text on an accent button. One shared table would get one of
    ''' each pair wrong.
    ''' </remarks>
    Public Module DesktopTheme

        Private Const FileName As String = "theme.txt"

        ' Original colours, captured on first theming so Light can restore precisely.
        Private ReadOnly _originals As New ConditionalWeakTable(Of Control, OriginalColors)

        Private _mode As ThemeMode = ThemeMode.Light

        ''' <summary>Raised after <see cref="Mode"/> changes, so open forms can re-theme.</summary>
        Public Event ThemeChanged As EventHandler

        Public ReadOnly Property Mode As ThemeMode
            Get
                Return _mode
            End Get
        End Property

        Public ReadOnly Property IsDark As Boolean
            Get
                Return _mode = ThemeMode.Dark
            End Get
        End Property

        ' ---- dark palette -----------------------------------------------------------------

        Private ReadOnly DarkPage As Color = Color.FromArgb(22, 27, 34)
        Private ReadOnly DarkCard As Color = Color.FromArgb(30, 36, 48)
        Private ReadOnly DarkInput As Color = Color.FromArgb(24, 30, 40)
        Private ReadOnly DarkText As Color = Color.FromArgb(229, 231, 235)
        Private ReadOnly DarkBorder As Color = Color.FromArgb(75, 85, 99)

        ' ---- persistence ------------------------------------------------------------------

        Private ReadOnly Property SettingsPath As String
            Get
                Return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GMS", FileName)
            End Get
        End Property

        ''' <summary>Loads the saved preference. Call once at start-up, before any form is shown.</summary>
        Public Sub Initialise()
            Try
                If File.Exists(SettingsPath) Then
                    Dim saved = File.ReadAllText(SettingsPath).Trim()
                    If String.Equals(saved, "Dark", StringComparison.OrdinalIgnoreCase) Then
                        _mode = ThemeMode.Dark
                    End If
                End If
            Catch
                ' A malformed or unreadable preference is not worth failing start-up over;
                ' the default (light) is always safe.
            End Try
        End Sub

        Private Sub Save()
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath))
                File.WriteAllText(SettingsPath, _mode.ToString())
            Catch
                ' Non-fatal: the choice still applies for this session.
            End Try
        End Sub

        Public Sub Toggle()
            _mode = If(_mode = ThemeMode.Dark, ThemeMode.Light, ThemeMode.Dark)
            Save()
            RaiseEvent ThemeChanged(Nothing, EventArgs.Empty)
        End Sub

        ' ---- applying ---------------------------------------------------------------------

        ''' <summary>
        ''' Themes <paramref name="form"/> now and again whenever the mode changes. Call once,
        ''' straight after <c>InitializeComponent()</c>.
        ''' </summary>
        Public Sub Attach(form As Form)
            If form Is Nothing Then Return

            Dim handler As EventHandler =
                Sub()
                    If form.IsDisposed OrElse Not form.IsHandleCreated Then Return
                    ApplyTo(form)
                    form.Invalidate(True)
                End Sub

            AddHandler ThemeChanged, handler
            ' Without this the module (a process-wide singleton) would hold every form it ever
            ' themed alive for the life of the application.
            AddHandler form.Disposed, Sub() RemoveHandler ThemeChanged, handler

            ApplyTo(form)
        End Sub

        ''' <summary>
        ''' Themes a control tree created after its form was attached - MainForm swaps a fresh
        ''' UserControl into the content area on every navigation, and those are built long
        ''' after <see cref="Attach"/> ran.
        ''' </summary>
        Public Sub Apply(control As Control)
            If control IsNot Nothing Then ApplyTo(control)
        End Sub

        ' Colours for code that sets a control's appearance itself and so cannot rely on the
        ' walker - the sidebar's selected-item highlight is rebuilt on every navigation.
        Public ReadOnly Property SidebarBack As Color
            Get
                Return If(IsDark, Color.FromArgb(11, 18, 32), UiKit.Sidebar)
            End Get
        End Property

        Public ReadOnly Property SidebarHoverBack As Color
            Get
                Return If(IsDark, Color.FromArgb(30, 41, 59), UiKit.SidebarHover)
            End Get
        End Property

        Public ReadOnly Property NavTextActive As Color
            Get
                Return Color.White
            End Get
        End Property

        Public ReadOnly Property NavTextInactive As Color
            Get
                Return If(IsDark, Color.FromArgb(203, 213, 225), Color.Gainsboro)
            End Get
        End Property

        Private Sub ApplyTo(control As Control)
            Dim original = _originals.GetValue(control, Function(c) New OriginalColors(c.BackColor, c.ForeColor))

            If _mode = ThemeMode.Light Then
                control.BackColor = original.Back
                control.ForeColor = original.Fore
            Else
                control.BackColor = MapBack(original.Back)
                control.ForeColor = MapFore(original.Fore)
            End If

            ApplyPerType(control, original)

            For Each child As Control In control.Controls
                ApplyTo(child)
            Next
        End Sub

        ''' <summary>Controls that need more than a background and foreground swap.</summary>
        Private Sub ApplyPerType(control As Control, original As OriginalColors)
            Dim dark = _mode = ThemeMode.Dark

            Dim input = TryCast(control, TextBoxBase)
            If input IsNot Nothing OrElse TypeOf control Is ComboBox OrElse
               TypeOf control Is NumericUpDown OrElse TypeOf control Is DateTimePicker OrElse
               TypeOf control Is ListBox Then
                ' Editable controls default to SystemColors.Window, which is white in both
                ' Windows themes, so they have to be set explicitly rather than mapped.
                control.BackColor = If(dark, DarkInput, original.Back)
                control.ForeColor = If(dark, DarkText, original.Fore)
                Return
            End If

            Dim button = TryCast(control, Button)
            If button IsNot Nothing Then
                button.FlatAppearance.MouseOverBackColor =
                    If(dark, MapBack(button.FlatAppearance.MouseOverBackColor), button.FlatAppearance.MouseOverBackColor)
                If button.FlatAppearance.BorderSize > 0 Then
                    button.FlatAppearance.BorderColor =
                        If(dark, DarkBorder, button.FlatAppearance.BorderColor)
                End If
                Return
            End If

            Dim grid = TryCast(control, DataGridView)
            If grid IsNot Nothing Then
                ' The grid carries its colours on style objects rather than the control, so the
                ' generic back/fore swap above misses almost all of it.
                grid.BackgroundColor = If(dark, DarkPage, original.Back)
                grid.GridColor = If(dark, DarkBorder, SystemColors.ControlDark)

                grid.DefaultCellStyle.BackColor = If(dark, DarkCard, Color.White)
                grid.DefaultCellStyle.ForeColor = If(dark, DarkText, Color.Black)
                grid.DefaultCellStyle.SelectionBackColor =
                    If(dark, Color.FromArgb(30, 58, 95), Color.FromArgb(219, 234, 254))
                grid.DefaultCellStyle.SelectionForeColor = If(dark, DarkText, Color.Black)

                grid.ColumnHeadersDefaultCellStyle.BackColor =
                    If(dark, Color.FromArgb(26, 34, 51), Color.FromArgb(249, 250, 251))
                grid.ColumnHeadersDefaultCellStyle.ForeColor = If(dark, DarkText, Color.Black)
                Return
            End If
        End Sub

        ' ---- colour maps ------------------------------------------------------------------

        Private Function MapBack(c As Color) As Color
            Select Case ToKey(c)
                Case ToKey(Color.White) : Return DarkCard
                Case ToKey(Color.FromArgb(243, 244, 246)) : Return DarkPage          ' page background
                Case ToKey(Color.FromArgb(249, 250, 251)) : Return Color.FromArgb(26, 34, 51)   ' grid header
                Case ToKey(Color.FromArgb(17, 24, 39)) : Return Color.FromArgb(11, 18, 32)      ' sidebar
                Case ToKey(Color.FromArgb(31, 41, 55)) : Return Color.FromArgb(30, 41, 59)      ' sidebar hover
                Case ToKey(Color.FromArgb(219, 234, 254)) : Return Color.FromArgb(30, 58, 95)   ' selection
                Case ToKey(Color.FromArgb(37, 99, 235)) : Return Color.FromArgb(59, 130, 246)   ' accent
                Case ToKey(Color.FromArgb(30, 64, 175)) : Return Color.FromArgb(37, 99, 235)    ' accent hover
                Case ToKey(SystemColors.Control) : Return DarkPage
                Case ToKey(SystemColors.Window) : Return DarkInput
                Case Else : Return DarkPage
            End Select
        End Function

        Private Function MapFore(c As Color) As Color
            Select Case ToKey(c)
                ' White stays white: it is the text on accent buttons and the sidebar brand,
                ' both of which keep a strong background in dark mode.
                Case ToKey(Color.White) : Return Color.White
                Case ToKey(Color.FromArgb(107, 114, 128)) : Return Color.FromArgb(154, 166, 184) ' muted
                Case ToKey(Color.FromArgb(31, 41, 55)) : Return DarkText                         ' body text
                Case ToKey(Color.Black) : Return DarkText
                Case ToKey(Color.FromArgb(37, 99, 235)) : Return Color.FromArgb(96, 165, 250)    ' accent text
                Case ToKey(Color.FromArgb(185, 28, 28)) : Return Color.FromArgb(248, 113, 113)   ' error red
                Case ToKey(Color.FromArgb(147, 197, 253)) : Return Color.FromArgb(125, 211, 252) ' sidebar link
                Case ToKey(Color.Gainsboro) : Return Color.FromArgb(203, 213, 225)               ' sidebar nav
                Case ToKey(SystemColors.ControlText) : Return DarkText
                Case ToKey(SystemColors.WindowText) : Return DarkText
                Case Else : Return DarkText
            End Select
        End Function

        ''' <summary>
        ''' Compares by ARGB value only. Color.White and Color.FromArgb(255,255,255) are equal
        ''' in colour but not by <c>Equals</c>, which also compares the named-colour flag.
        ''' </summary>
        Private Function ToKey(c As Color) As Integer
            Return c.ToArgb()
        End Function

        Private NotInheritable Class OriginalColors
            Public ReadOnly Back As Color
            Public ReadOnly Fore As Color

            Public Sub New(back As Color, fore As Color)
                Me.Back = back
                Me.Fore = fore
            End Sub
        End Class

    End Module

End Namespace
