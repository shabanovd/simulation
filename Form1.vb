Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices

Imports System.Runtime.InteropServices.Marshal

Imports OpenCL.Net.Extensions
Imports OpenCL.Net

Public Class Form1

    Dim CT As Integer = 0

    Dim CP As Integer = 0

    ' Продолжительность следа спонтанной активности
    Dim NAT As Integer = 4

    ' Продолжительность тишины
    Dim PASSIVE As Integer = NAT * 5

    Dim X_C As Integer = 100
    Dim Y_C As Integer = 100

    Dim pic As New Bitmap(X_C, Y_C)

    ' Кол-во нейронов в патерне вызванной активности
    Dim NRAN As Integer = 30

    ' Радиус паттерна вызванной активности
    Dim RRAP As Integer = 6

    ' Расстояние слежения за спонтанной активностью
    Dim LSA As Integer = 15


    ' Порог активации по идентификатору
    Dim A_min As Double = 0.25

    Dim NSpN As Integer = X_C * Y_C * 0.07

    ' Общее количество паттернов
    Dim NP As Integer = 1

    ' Количество активных паттернов в режиме волны
    Dim NPC As Integer = 1

    'константа максимального кол-ва волн пересечения
    Dim Const_PC As Double = 4

    ' Набор паттернов
    Dim Cortex(X_C - 1, Y_C - 1, NP - 1) As Byte


    Dim CortexA(X_C - 1, Y_C - 1) As Byte
    Dim CortexT(X_C - 1, Y_C - 1) As Integer

    Dim __CortexA(X_C - 1, Y_C - 1) As Byte
    Dim __CortexT(X_C - 1, Y_C - 1) As Integer

    ' Центры паттернов
    Dim C(NP - 1, 2) As Integer


    Dim NM As Integer = 20

    Dim memP(X_C - 1, Y_C - 1, NM - 1) As Byte

    Dim memI(NP - 1, NM - 1) As Boolean

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        For i = 0 To NP - 1

            C(i, 0) = (X_C - 3 * RRAP) * Rnd() + RRAP
            C(i, 1) = (Y_C - 3 * RRAP) * Rnd() + RRAP

        Next

    End Sub

    Dim kernel As OpenCL.Net.Kernel

    Dim sizeOfCortexA As Integer
    Dim sizeOfCortexT As Integer
    Dim sizeOfCortex As Integer

    Dim bufferOfCortex As IMem
    Dim bufferOfCortexA As IMem
    Dim bufferOfCortexT As IMem

    Dim bufferOfImg As IMem
    
    Dim imgBytesSize As Integer = X_C * Y_C * 4
    Dim outputByteArray(imgBytesSize - 1) As Byte

    Dim originPtr() As IntPtr = {New IntPtr(0), New IntPtr(0), New IntPtr(0)}
    Dim regionPtr() As IntPtr = {New IntPtr(X_C), New IntPtr(Y_C), New IntPtr(1)}

    Dim cmdQueue As CommandQueue

    Dim sizeOfByte As Integer = SizeOf(GetType(Byte))
    Dim sizeOfDouble As Integer = SizeOf(GetType(Double))
    Dim sizeOfInt As Integer = SizeOf(GetType(Integer))
    Dim sizeOfIntPtr As Integer = SizeOf(GetType(IntPtr))


    Private Sub CheckErr(ByRef err As ErrorCode, ByRef name As String)
        If (err <> ErrorCode.Success) Then
            MsgBox("ERROR: " + name + " (" + err.ToString() + ")")
        End If
    End Sub

    Private Sub initPU()

        Dim err As ErrorCode
        Dim platforms() As Platform = Cl.GetPlatformIDs(err)

        'MsgBox(Cl.GetPlatformInfo(platforms(1), PlatformInfo.Name, err).ToString())

        Dim devices() As Device = Cl.GetDeviceIDs(platforms(0), DeviceType.Cpu, err)
        Dim device = devices(0) 'cl_device_id device

        Dim res = Cl.GetDeviceInfo(device, DeviceInfo.ImageSupport, err)
        'MsgBox(res.ToString)

        Dim context As Context = Cl.CreateContext(Nothing, 1, devices, Nothing, IntPtr.Zero, err)
        cmdQueue = Cl.CreateCommandQueue(context, device, CommandQueueProperties.None, err)

        'Create and build a program from our OpenCL-C source code
        Dim programSource As String = File.ReadAllText("functions.cl")

        Dim program As Program = Cl.CreateProgramWithSource(context, 1, {programSource}, Nothing, err)
        'CheckErr(err, "CreateProgramWithSource")
        Cl.BuildProgram(program, 0, Nothing, String.Empty, Nothing, IntPtr.Zero)  '"-cl-mad-enable"

        'Check for any compilation errors
        'If (
        Dim status = Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Status, err) '.CastTo(BuildStatus) <> BuildStatus.Success) Then
        ' Then
        'If (err <> ErrorCode.Success) Then
        'MsgBox("ERROR: " + "Cl.GetProgramBuildInfo" + " (" + err.ToString() + ")")
        'Console.WriteLine("Cl.GetProgramBuildInfo != Success")
        MsgBox(Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, err).ToString)
        'End If
        'End If


        'Create a kernel from our program
        kernel = Cl.CreateKernel(program, "Do", err)

        'Get the maximum number of work items supported for this kernel on this device
        'Dim notused As IntPtr
        'Dim local As InfoBuffer = New InfoBuffer(New IntPtr(4))
        'Cl.GetKernelWorkGroupInfo(kernel, device, KernelWorkGroupInfo.WorkGroupSize, New IntPtr(sizeOfInt), local, notused)

        sizeOfCortexA = X_C * Y_C * sizeOfByte

        sizeOfCortex = X_C * Y_C * NP * sizeOfByte

        sizeOfCortexT = X_C * Y_C * sizeOfInt

        bufferOfCortex = Cl.CreateBuffer(context, MemFlags.ReadWrite, sizeOfCortex, err)
        bufferOfCortexA = Cl.CreateBuffer(context, MemFlags.ReadWrite, sizeOfCortexA, err)
        bufferOfCortexT = Cl.CreateBuffer(context, MemFlags.ReadWrite, sizeOfCortexT, err)

        Dim clImageFormat = New OpenCL.Net.ImageFormat(ChannelOrder.RGBA, ChannelType.Unsigned_Int8)

        bufferOfImg = Cl.CreateImage2D(context, MemFlags.CopyHostPtr Or MemFlags.WriteOnly, clImageFormat, X_C, Y_C, 0, outputByteArray, err)
        'MsgBox(err)

        Dim event0 As [Event]

        Cl.EnqueueWriteBuffer(cmdQueue, bufferOfCortex, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortex), Cortex, 0, Nothing, event0)

    End Sub

    Private Sub prepare()

        Dim event0 As [Event]

        'Cl.EnqueueWriteBuffer(cmdQueue, bufferOfCortexA, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexA), CortexA, 0, Nothing, event0)
        'Cl.EnqueueWriteBuffer(cmdQueue, bufferOfCortexT, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexT), CortexT, 0, Nothing, event0)
        Dim msg

        msg = Cl.SetKernelArg(kernel, 0, New IntPtr(sizeOfIntPtr), bufferOfImg)
        msg = Cl.SetKernelArg(kernel, 1, New IntPtr(sizeOfInt), CT)
        msg = Cl.SetKernelArg(kernel, 2, New IntPtr(sizeOfIntPtr), bufferOfCortexA)
        msg = Cl.SetKernelArg(kernel, 3, New IntPtr(sizeOfIntPtr), bufferOfCortexT)
        msg = Cl.SetKernelArg(kernel, 4, New IntPtr(sizeOfIntPtr), bufferOfCortex)
        msg = Cl.SetKernelArg(kernel, 5, New IntPtr(sizeOfInt), X_C)
        msg = Cl.SetKernelArg(kernel, 6, New IntPtr(sizeOfInt), Y_C)
        msg = Cl.SetKernelArg(kernel, 7, New IntPtr(sizeOfInt), NM)

        Dim sizePtr() As IntPtr = {New IntPtr(X_C), New IntPtr(Y_C), New IntPtr(NP)}
        msg = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 3, Nothing, sizePtr, Nothing, 0, Nothing, event0)

        'Force the command queue to get processed, wait until all commands are complete
        'Cl.Finish(cmdQueue)

        'Cl.EnqueueReadBuffer(cmdQueue, bufferOfCortex, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortex), Cortex, UInteger.Parse("0"), Nothing, event0)
        'Cl.EnqueueReadBuffer(cmdQueue, bufferOfCortexA, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexA), CortexA, UInteger.Parse("0"), Nothing, event0)
        'Cl.EnqueueReadBuffer(cmdQueue, bufferOfCortexT, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexT), CortexT, UInteger.Parse("0"), Nothing, event0)

        msg = Cl.EnqueueReadImage(cmdQueue, bufferOfImg, Bool.True, originPtr, regionPtr, New IntPtr(0), New IntPtr(0), outputByteArray, 0, Nothing, event0)

        Cl.Finish(cmdQueue)

        Dim pinnedOutputArray As GCHandle = GCHandle.Alloc(outputByteArray, GCHandleType.Pinned)
        Dim outputBmpPointer As IntPtr = pinnedOutputArray.AddrOfPinnedObject()
        'Create a new bitmap with processed data and save it to a file.
        Dim outputBitmap As Bitmap = New Bitmap(X_C, Y_C, X_C * 4, PixelFormat.Format32bppArgb, outputBmpPointer)

        PictureBox1.Image = outputBitmap
        PictureBox1.Update()

    End Sub


    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        ClrPic()

        For i = 0 To NP - 1

            MakeP(i)

            MakePicSp(i)

        Next

        For i = 0 To NP - 1

            MakePicR(i)

        Next

        initPU()

        PictureBox1.Image = pic

    End Sub

    Private Sub MakeP(ByRef CurP As Integer)

        Dim Ang As Double
        Dim R As Double


        For i = 0 To NSpN - 1


            cortex(Rnd() * (X_C - 1), Rnd() * (Y_C - 1), CurP) = 2

        Next


        For i = 0 To NRAN - 1

            Ang = 2 * Math.PI * Rnd()
            R = RRAP * Rnd()

            cortex(C(CurP, 0) + R * Math.Sin(Ang), C(CurP, 1) + R * Math.Cos(Ang), CurP) = 1

        Next
    End Sub

    Private Sub ClrPic()

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1

                pic.SetPixel(i, j, Color.Black)

            Next
        Next
    End Sub

    Private Sub MakePicR(CurP As Integer)

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1

                If cortex(i, j, CurP) = 1 Then
                    pic.SetPixel(i, j, Color.Blue)
                End If

            Next
        Next
    End Sub

    Private Sub MakePicSp(CurP As Integer)

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1

                If cortex(i, j, CurP) = 2 Then
                    If CurP > NPC Then
                        pic.SetPixel(i, j, Color.DarkBlue)
                    Else
                        pic.SetPixel(i, j, Color.DarkBlue)
                    End If
                End If

            Next
        Next
    End Sub

    Private Sub MakePicWave()

        Dim Y As Integer

        For ix = 0 To X_C - 1
            For iy = 0 To Y_C - 1


                Select Case CortexA(ix, iy)

                    'Case -1

                    '    pic.SetPixel(ix, iy, Color.Yellow)

                    Case 1
                        pic.SetPixel(ix, iy, Color.Red)

                    Case 2

                        Y = Math.Min((((1 - (CT - CortexT(ix, iy)) / NAT)) / 2 + 0.5) * 255, 255)

                        pic.SetPixel(ix, iy, Drawing.Color.FromArgb(Y, Y, Y))

                    Case 3

                        'If Cortex(ix, iy, 0) > 0 Then
                        pic.SetPixel(ix, iy, Color.White)
                        'Else
                        'pic.SetPixel(ix, iy, Color.LightBlue)
                        'End If

                    Case 4

                        Dim notFound = True

                        For p = 0 To NP - 1
                            If Cortex(ix, iy, p) = 1 Then
                                If p = 1 Then
                                    pic.SetPixel(ix, iy, Color.Orange)
                                Else
                                    pic.SetPixel(ix, iy, Color.DarkRed)
                                End If
                                notFound = False
                                Exit For
                            End If
                        Next

                        If notFound Then
                            pic.SetPixel(ix, iy, Color.DarkBlue)
                        End If


                    Case Else

                        Dim notFound = True

                        'For p = 0 To NP - 1
                        '    If Cortex(ix, iy, p) = 1 Then
                        '        If p = 1 Then
                        '            pic.SetPixel(ix, iy, Color.Yellow)
                        '        Else
                        '            pic.SetPixel(ix, iy, Color.LightYellow)
                        '        End If
                        '        notFound = False
                        '        Exit For
                        '    End If
                        'Next


                        If notFound Then
                            'If __CortexA(ix, iy) > 0 Then
                            '    pic.SetPixel(ix, iy, Color.LightSkyBlue)
                            'Else
                            pic.SetPixel(ix, iy, Color.Black)
                            'End If
                        End If

                End Select

            Next
        Next
    End Sub


    'Private Sub PictureBox1_Click(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles PictureBox1.MouseClick
    '    Dim X, Y As Integer


    '    X = Int((e.X) / Me.PictureBox1.Width * X_C)
    '    Y = Int((e.Y) / Me.PictureBox1.Height * Y_C)


    '    NP += 1

    '    ReDim Preserve cortex(X_C, Y_C, NP)



    'End Sub

    ' Распространение волны
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        Wave(False)

    End Sub

    Private Sub __Wave(M As Boolean)

        gpu_Wave(M)

        vb_Wave(M)

        Dim err0 As Integer = 0
        Dim err1 As Integer = 0
        Dim err2 As Integer = 0

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1
                If CortexA(i, j) <> __CortexA(i, j) Then

                    'If (i = 687 And j = 521) Then
                    '    Dim a = 0
                    'End If

                    If CortexA(i, j) = 0 Then
                        err0 += 1
                    End If

                    err1 += 1
                End If
                If CortexT(i, j) <> __CortexT(i, j) Then
                    err2 += 1
                End If
            Next
        Next

        Label2.Text = err0.ToString & " " & err1.ToString
        Label3.Text = err2.ToString

        MakePicWave()

        PictureBox1.Image = pic
        PictureBox1.Update()

        CT += 1

    End Sub

    Private Sub Wave(M As Boolean)
        Dim TS = DateTime.UtcNow

        For n = 0 To 1000

            TS = DateTime.UtcNow

            gpu_Wave(M)

            Label2.Text = DateTime.UtcNow.Subtract(TS).ToString
            Label2.Update()

            'TS = DateTime.UtcNow

            'MakePicWave()

            'PictureBox1.Image = pic
            'PictureBox1.Update()

            'Label3.Text = DateTime.UtcNow.Subtract(TS).ToString
            'Label3.Update()

            CT += 1
        Next
    End Sub

    Private Sub gpu_Wave(M As Boolean)
        prepare()

        'activation()
    End Sub


    Private Sub vb_Wave(M As Boolean)

        'For n = 0 To 1999
        For i = LSA To X_C - LSA
            For j = LSA To Y_C - LSA

                If __CortexA(i, j) = 3 Then

                    __CortexA(i, j) = 2

                End If

                If __CortexA(i, j) = 2 And CT - __CortexT(i, j) > NAT Then

                    __CortexT(i, j) = CT
                    __CortexA(i, j) = 4

                End If

            Next
        Next


        For ix = LSA To X_C - LSA
            For iy = LSA To Y_C - LSA

                For i = 0 To NP - 1


                    Act(ix, iy, i, Cortex)


                Next

            Next
        Next


        ' Активация по памяти на события
        If M Then



            For ix = LSA To X_C - LSA
                For iy = LSA To Y_C - LSA

                    For i = 0 To NM - 1
                        'For i = 1 To 1

                        If memP(ix, iy, i) = 1 Then
                            'If (CortexA(ix, iy) = -1) Then
                            Act(ix, iy, i, memP)
                            'End If
                        End If

                    Next

                Next
            Next
        End If

    End Sub

    Private Sub Act(i As Integer, j As Integer, k As Integer, ByRef C(,,) As Byte)

        Dim NAct As Integer
        Dim NActR As Double

        Dim NFAct As Integer


        NAct = 0
        NActR = 0
        NFAct = 0


        If C(i, j, k) > 0 And (__CortexA(i, j) <= 0 Or (__CortexA(i, j) = 4 And CT - __CortexT(i, j) > PASSIVE)) Then

            For i1 = i - LSA To i + LSA
                For j1 = j - LSA To j + LSA

                    If i1 >= 0 And i1 < X_C And j1 >= 0 And j1 < Y_C Then


                        If C(i1, j1, k) > 0 Then

                            NActR += 1

                            If (__CortexA(i1, j1) = 1 Or __CortexA(i1, j1) = 2) Then
                                'If ((CortexA(i1, j1) = 1 And CortexA(i, j) <> 4) Or CortexA(i1, j1) = 2) Then

                                NAct += 1

                            End If

                        Else
                            If (__CortexA(i1, j1) = 1 Or __CortexA(i1, j1) = 2) Then
                                NFAct += 1
                            End If
                        End If

                    End If


                Next
            Next

            'If (i = 687 And j = 521) Then
            '    Dim a = 0
            'End If

            If NActR > 0 Then

                If (NAct / NActR > A_min) And (NFAct / Const_PC < NAct) Then

                    __CortexT(i, j) = CT
                    __CortexA(i, j) = 3

                End If

            End If

        End If


    End Sub

    ' Задание паттерна вызванной активности
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1

                CortexA(i, j) = 0
                __CortexA(i, j) = 0

                For k = 0 To NPC - 1

                    If Cortex(i, j, k) = 1 Then

                        CortexA(i, j) = 1
                        __CortexA(i, j) = 1


                    End If

                Next
            Next
        Next

        Dim event0 As [Event]

        Cl.EnqueueWriteBuffer(cmdQueue, bufferOfCortexA, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexA), CortexA, 0, Nothing, event0)
        Cl.EnqueueWriteBuffer(cmdQueue, bufferOfCortexT, Bool.True, IntPtr.Zero, New IntPtr(sizeOfCortexT), CortexT, 0, Nothing, event0)

        MakePicWave()

        PictureBox1.Image = pic


    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click

        Dim TP As Integer = 0
        Dim FP As Integer = 0

        Dim TN As Integer = 0
        Dim FN As Integer = 0

        Dim F As Boolean

        For i = LSA To X_C - LSA
            For j = LSA To Y_C - LSA

                If CortexA(i, j) >= 2 Then

                    F = False

                    For k = 0 To NPC - 1

                        If Cortex(i, j, k) > 0 Then
                            F = True
                            Exit For
                        End If

                    Next

                    If F Then
                        TP += 1
                    Else
                        FP += 1

                    End If

                Else
                    F = False

                    For k = 0 To NPC - 1

                        If Cortex(i, j, k) > 0 Then
                            F = True
                            Exit For
                        End If

                    Next

                    If F Then
                        FN += 1
                    Else
                        TN += 1

                    End If


                End If



            Next
        Next


        Label1.Text = TP.ToString + "/" + FP.ToString + " " + TN.ToString + "/" + FN.ToString


    End Sub



    ' Задание воспоминаний, как комбинаций паттернов понятий
    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click

        For i = 0 To NM - 1

            For j = 0 To NP - 1

                If Rnd() > 0.8 Then

                    memI(j, i) = True

                    For ix = 0 To X_C - 1
                        For iy = 0 To Y_C - 1

                            If Cortex(ix, iy, j) > 0 Then

                                If memP(ix, iy, i) <> 1 Then

                                    memP(ix, iy, i) = Cortex(ix, iy, j)

                                End If
                            End If
                        Next
                    Next
                End If
            Next
        Next

    End Sub

    ' Выбор неполного воспоминания. Пропускаем первое понятие
    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click

        CP += 1

        Dim N As Integer

        For i = 0 To X_C - 1
            For j = 0 To Y_C - 1

                CortexA(i, j) = 0

                N = 0

                For k = 0 To NP - 1

                    If memI(k, CP) Then

                        N += 1

                        If Cortex(i, j, k) = 1 Then

                            If N = 1 Then

                                'CortexA(i, j) = 0

                            Else

                                CortexA(i, j) = 1

                            End If

                        End If


                    End If

                Next
            Next
        Next

        MakePicWave()

        PictureBox1.Image = pic

    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click

        Wave(True)

    End Sub
End Class
