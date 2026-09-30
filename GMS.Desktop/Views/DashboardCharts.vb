Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms
Imports GMS.Core.Contracts
Imports GMS.Desktop.App

Namespace Views

    ''' <summary>
    ''' Shared look for the dashboard's owner-drawn charts. Both charts paint their entire surface
    ''' from these values rather than from BackColor/ForeColor, so the theme walker recolouring
    ''' the control tree cannot leave one half light and the other half dark.
    ''' </summary>
    Friend Module ChartStyle
        Public ReadOnly Property Surface As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(30, 36, 48), Color.White)
            End Get
        End Property

        Public ReadOnly Property Ink As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(229, 231, 235), Color.FromArgb(17, 24, 39))
            End Get
        End Property

        Public ReadOnly Property Muted As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(154, 166, 184), Color.FromArgb(100, 116, 139))
            End Get
        End Property

        Public ReadOnly Property Series As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(59, 130, 246), Color.FromArgb(37, 99, 235))
            End Get
        End Property

        Public ReadOnly Property Grid As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(40, 52, 76), Color.FromArgb(237, 241, 247))
            End Get
        End Property

        Public ReadOnly Property Track As Color
            Get
                Return If(DesktopTheme.IsDark, Color.FromArgb(38, 48, 72), Color.FromArgb(238, 242, 249))
            End Get
        End Property

        Public Function Compact(v As Double) As String
            If v >= 1000000.0 Then Return (v / 1000000.0).ToString("0.#", CultureInfo.CurrentCulture) & "M"
            If v >= 1000.0 Then Return (v / 1000.0).ToString("0.#", CultureInfo.CurrentCulture) & "k"
            Return v.ToString("0", CultureInfo.CurrentCulture)
        End Function

        ''' <summary>Rounds up to 1, 2 or 5 times a power of ten so gridlines land on numbers people would pick.</summary>
        Public Function NiceMax(value As Double) As Double
            If value <= 0 Then Return 100
            Dim mag = Math.Pow(10, Math.Floor(Math.Log10(value)))
            Dim f = value / mag
            Return If(f <= 1, 1, If(f <= 2, 2, If(f <= 5, 5, 10))) * mag
        End Function

        Public Sub DrawHeader(g As Graphics, title As String, subtitle As String, Optional right As String = Nothing)
            Using f As New Font("Segoe UI Semibold", 10.5F), f2 As New Font("Segoe UI", 8.5F),
                  inkB As New SolidBrush(Ink), mutB As New SolidBrush(Muted)
                g.DrawString(title, f, inkB, 16, 12)
                g.DrawString(subtitle, f2, mutB, 16, 34)
                If right IsNot Nothing Then
                    Using f3 As New Font("Segoe UI Semibold", 14.0F)
                        Dim w = g.MeasureString(right, f3).Width
                        Dim x = CSng(g.VisibleClipBounds.Right) - w - 12
                        g.DrawString(right, f3, inkB, x, 10)
                    End Using
                End If
            End Using
        End Sub
    End Module

    ''' <summary>Confirmed sales per day as an area line, with a crosshair and value tooltip on hover.</summary>
    Friend NotInheritable Class SalesTrendChart
        Inherits Control

        Private Const Left As Integer = 52, Right As Integer = 16, Top As Integer = 62, Bottom As Integer = 28
        Private _points As IReadOnlyList(Of DailySalesPoint) = New List(Of DailySalesPoint)()
        Private _days As Integer
        Private _hover As Integer = -1

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            MinimumSize = New Size(320, 200)
        End Sub

        Public Sub SetData(charts As DashboardCharts)
            _points = charts.DailySales
            _days = charts.Days
            _hover = -1
            Invalidate()
        End Sub

        Private Function PlotRect() As Rectangle
            Return New Rectangle(Left, Top, Math.Max(10, Width - Left - Right), Math.Max(10, Height - Top - Bottom))
        End Function

        Private Function XAt(i As Integer, plot As Rectangle) As Single
            If _points.Count <= 1 Then Return plot.Left + plot.Width / 2.0F
            Return plot.Left + CSng(i) * plot.Width / (_points.Count - 1)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
            g.Clear(ChartStyle.Surface)
            Using pen As New Pen(DesktopTheme.CardBorder)
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
            End Using

            Dim total = _points.Sum(Function(p) p.Total)
            Dim count = _points.Sum(Function(p) p.TransactionCount)
            ChartStyle.DrawHeader(g, "Sales trend", $"Confirmed sales per day · last {_days} days",
                                  total.ToString("N2") & "  ")
            If count = 0 Then
                Using f As New Font("Segoe UI", 9.5F), b As New SolidBrush(ChartStyle.Muted)
                    Dim msg = $"No confirmed sales in the last {_days} days."
                    Dim sz = g.MeasureString(msg, f)
                    g.DrawString(msg, f, b, (Width - sz.Width) / 2, Height / 2.0F)
                End Using
                Return
            End If

            Dim plot = PlotRect()
            Dim niceMax = ChartStyle.NiceMax(CDbl(_points.Max(Function(p) p.Total)))
            Dim yOf = Function(v As Decimal) As Single
                          Return plot.Bottom - CSng(CDbl(v) / niceMax) * plot.Height
                      End Function

            Using tick As New Font("Segoe UI", 8.0F), muted As New SolidBrush(ChartStyle.Muted),
                  grid As New Pen(ChartStyle.Grid), axis As New Pen(ChartStyle.Grid, 1.5F)
                Dim right As New StringFormat With {.Alignment = StringAlignment.Far, .LineAlignment = StringAlignment.Center}
                For k = 0 To 4
                    Dim y = plot.Bottom - plot.Height * k / 4.0F
                    g.DrawLine(If(k = 0, axis, grid), plot.Left, y, plot.Right, y)
                    g.DrawString(ChartStyle.Compact(niceMax * k / 4.0), tick, muted, New RectangleF(0, y - 8, Left - 8, 16), right)
                Next
                For Each i In New Integer() {0, _points.Count \ 2, _points.Count - 1}.Distinct()
                    Dim fmt As New StringFormat With {.Alignment = If(i = 0, StringAlignment.Near,
                                                       If(i = _points.Count - 1, StringAlignment.Far, StringAlignment.Center))}
                    Dim x = XAt(i, plot)
                    Dim box = If(i = 0, New RectangleF(x, plot.Bottom + 6, 70, 16),
                                 If(i = _points.Count - 1, New RectangleF(x - 70, plot.Bottom + 6, 70, 16),
                                    New RectangleF(x - 35, plot.Bottom + 6, 70, 16)))
                    g.DrawString(_points(i).Day.ToString("d MMM", CultureInfo.CurrentCulture), tick, muted, box, fmt)
                Next
            End Using

            Dim pts = _points.Select(Function(p, i) New PointF(XAt(i, plot), yOf(p.Total))).ToArray()
            If pts.Length > 1 Then
                Dim area = pts.Concat({New PointF(pts(pts.Length - 1).X, plot.Bottom), New PointF(pts(0).X, plot.Bottom)}).ToArray()
                Using br As New LinearGradientBrush(New Rectangle(plot.Left, plot.Top, plot.Width, plot.Height),
                                                    Color.FromArgb(70, ChartStyle.Series), Color.FromArgb(0, ChartStyle.Series), 90.0F)
                    g.FillPolygon(br, area)
                End Using
                Using pen As New Pen(ChartStyle.Series, 2.0F) With {.LineJoin = LineJoin.Round}
                    g.DrawLines(pen, pts)
                End Using
            End If

            If _hover >= 0 AndAlso _hover < pts.Length Then
                Dim hp = pts(_hover)
                Using dash As New Pen(ChartStyle.Muted, 1.0F) With {.DashStyle = DashStyle.Dot}
                    g.DrawLine(dash, hp.X, plot.Top, hp.X, plot.Bottom)
                End Using
                Using ring As New SolidBrush(ChartStyle.Surface), fill As New SolidBrush(ChartStyle.Series)
                    g.FillEllipse(ring, hp.X - 6, hp.Y - 6, 12, 12)
                    g.FillEllipse(fill, hp.X - 4, hp.Y - 4, 8, 8)
                End Using
                DrawTooltip(g, _points(_hover), hp, plot)
            End If
        End Sub

        Private Sub DrawTooltip(g As Graphics, p As DailySalesPoint, at As PointF, plot As Rectangle)
            Dim l1 = p.Day.ToString("ddd d MMM", CultureInfo.CurrentCulture)
            Dim l2 = p.Total.ToString("N2")
            Dim l3 = If(p.TransactionCount = 1, "1 sale", $"{p.TransactionCount:N0} sales")
            Using f1 As New Font("Segoe UI", 8.5F), f2 As New Font("Segoe UI Semibold", 10.0F),
                  ink As New SolidBrush(ChartStyle.Ink), mut As New SolidBrush(ChartStyle.Muted)
                Dim w = Math.Max(g.MeasureString(l1, f1).Width, Math.Max(g.MeasureString(l2, f2).Width, g.MeasureString(l3, f1).Width)) + 20
                Dim h = 62.0F
                Dim x = at.X + 12
                If x + w > Width - 4 Then x = at.X - w - 12
                Dim y = Math.Max(plot.Top - 4, Math.Min(at.Y - h / 2, plot.Bottom - h))
                Using bg As New SolidBrush(ChartStyle.Surface), border As New Pen(DesktopTheme.CardBorder)
                    g.FillRectangle(bg, x, y, w, h)
                    g.DrawRectangle(border, x, y, w, h)
                End Using
                g.DrawString(l1, f1, mut, x + 10, y + 6)
                g.DrawString(l2, f2, ink, x + 10, y + 21)
                g.DrawString(l3, f1, mut, x + 10, y + 42)
            End Using
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim plot = PlotRect()
            Dim idx = -1
            If _points.Count > 0 AndAlso e.Y >= plot.Top - 10 AndAlso e.Y <= plot.Bottom + 10 AndAlso
               e.X >= plot.Left - 6 AndAlso e.X <= plot.Right + 6 Then
                idx = If(_points.Count = 1, 0,
                         CInt(Math.Round((e.X - plot.Left) / CDbl(plot.Width) * (_points.Count - 1))))
                idx = Math.Max(0, Math.Min(_points.Count - 1, idx))
            End If
            If idx <> _hover Then
                _hover = idx
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hover <> -1 Then
                _hover = -1
                Invalidate()
            End If
        End Sub
    End Class

    ''' <summary>Best sellers by revenue as horizontal bars, drawn to a shared scale.</summary>
    Friend NotInheritable Class TopProductsChart
        Inherits Control

        Private _top As IReadOnlyList(Of TopProductPoint) = New List(Of TopProductPoint)()
        Private _days As Integer

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            MinimumSize = New Size(240, 200)
        End Sub

        Public Sub SetData(charts As DashboardCharts)
            _top = charts.TopProducts
            _days = charts.Days
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit
            g.Clear(ChartStyle.Surface)
            Using pen As New Pen(DesktopTheme.CardBorder)
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
            End Using
            ChartStyle.DrawHeader(g, "Top products", $"By revenue · last {_days} days")

            If _top.Count = 0 Then
                Using f As New Font("Segoe UI", 9.5F), b As New SolidBrush(ChartStyle.Muted)
                    Dim msg = $"Nothing sold in the last {_days} days."
                    Dim sz = g.MeasureString(msg, f)
                    g.DrawString(msg, f, b, (Width - sz.Width) / 2, Height / 2.0F)
                End Using
                Return
            End If

            Dim maxRev = _top.Max(Function(p) p.Revenue)
            Dim rowH = Math.Min(44.0F, (Height - 66.0F) / _top.Count)
            Dim left = 16.0F, w = Width - 32.0F
            Using nameF As New Font("Segoe UI", 9.0F), valF As New Font("Segoe UI Semibold", 9.0F),
                  noteF As New Font("Segoe UI", 8.0F), ink As New SolidBrush(ChartStyle.Ink),
                  mut As New SolidBrush(ChartStyle.Muted), track As New SolidBrush(ChartStyle.Track),
                  fill As New SolidBrush(ChartStyle.Series)
                Dim trim As New StringFormat With {.Trimming = StringTrimming.EllipsisCharacter, .FormatFlags = StringFormatFlags.NoWrap}
                Dim far As New StringFormat With {.Alignment = StringAlignment.Far}
                For i = 0 To _top.Count - 1
                    Dim p = _top(i)
                    Dim y = 62.0F + i * rowH
                    Dim valueText = p.Revenue.ToString("N2")
                    Dim valueW = g.MeasureString(valueText, valF).Width
                    g.DrawString(p.ProductName, nameF, ink, New RectangleF(left, y, w - valueW - 8, 18), trim)
                    g.DrawString(valueText, valF, ink, New RectangleF(left, y, w, 18), far)
                    Dim barY = y + 20
                    Using tp = RoundedBar(left, barY, w, 7)
                        g.FillPath(track, tp)
                    End Using
                    Dim fw = Math.Max(6.0F, CSng(p.Revenue / maxRev) * w)
                    Using fp = RoundedBar(left, barY, fw, 7)
                        g.FillPath(fill, fp)
                    End Using
                    g.DrawString($"{p.QuantitySold:0.##} sold", noteF, mut, left, barY + 8)
                Next
            End Using
        End Sub

        Private Shared Function RoundedBar(x As Single, y As Single, w As Single, h As Single) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim d = h
            path.AddArc(x, y, d, d, 90, 180)
            path.AddArc(x + w - d, y, d, d, 270, 180)
            path.CloseFigure()
            Return path
        End Function
    End Class

End Namespace
