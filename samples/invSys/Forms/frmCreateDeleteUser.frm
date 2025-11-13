VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmCreateDeleteUser 
   Caption         =   "UserForm1"
   ClientHeight    =   4080
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   6435
   OleObjectBlob   =   "frmCreateDeleteUser.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmCreateDeleteUser"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub UserForm_Initialize()
    ' Load roles into the dropdown
    modUserAuth.LoadRolesIntoComboBox Me.cmbRole
End Sub
Private Sub btnCreateUser_Click()
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim newRow As ListRow
    Dim username As String
    Dim pin As String
    Dim role As String
    Dim userID As String
    Dim foundUser As Range
    ' Set reference to UserCredentials table
    Set ws = ThisWorkbook.Sheets("UserCredentials")
    Set tbl = ws.ListObjects("UserCredentials")
    ' Get user input
    username = Trim(Me.txtUsername.value)
    pin = Trim(Me.txtPIN.value)
    role = Me.cmbRole.value
    ' Validate inputs
    If username = "" Or pin = "" Or role = "" Then
        Me.lblMessage.Caption = "All fields are required."
        Exit Sub
    End If
    If Not IsNumeric(pin) Or Len(pin) <> 6 Then
        Me.lblMessage.Caption = "PIN must be exactly 6 digits."
        Exit Sub
    End If
    ' Get column indexes dynamically
    Dim colUserID As Integer, colUsername As Integer, colPIN As Integer
    Dim colRole As Integer, colStatus As Integer, colLastLogin As Integer
    colUserID = tbl.ListColumns("USER_ID").Index
    colUsername = tbl.ListColumns("USERNAME").Index
    colPIN = tbl.ListColumns("PIN").Index
    colRole = tbl.ListColumns("ROLE").Index
    colStatus = tbl.ListColumns("STATUS").Index
    colLastLogin = tbl.ListColumns("LAST LOGIN").Index
    ' Check if username already exists
    Set foundUser = tbl.ListColumns("USERNAME").DataBodyRange.Find(What:=username, LookAt:=xlWhole)
    If Not foundUser Is Nothing Then
        Me.lblMessage.Caption = "User already exists."
        Exit Sub
    End If
    ' Generate unique USER_ID (e.g., "USR" & timestamp)
    userID = "USR" & Format(Now, "YYMMDDHHMMSS")
    ' Add new user to UserCredentials table
    Set newRow = tbl.ListRows.Add
    newRow.Range(1, colUserID).value = userID  ' USER_ID
    newRow.Range(1, colUsername).value = username  ' USERNAME
    newRow.Range(1, colPIN).value = pin  ' PIN
    newRow.Range(1, colRole).value = role  ' ROLE
    newRow.Range(1, colStatus).value = "Active"  ' STATUS
    newRow.Range(1, colLastLogin).value = ""  ' LAST LOGIN (empty)
    ' Confirmation message
    Me.lblMessage.Caption = "User created successfully!"
End Sub
Private Sub btnRandomPIN_Click()
    Dim randomPIN As String
    ' Generate a random 6-digit number
    randomPIN = Format(Int((900000 * Rnd) + 100000), "000000")
    ' Display the generated PIN in txtPIN
    Me.txtPIN.value = randomPIN
End Sub
Private Sub btnDeleteUser_Click()
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundUser As Range
    Dim username As String
    Dim colUsername As Integer
    ' Set reference to UserCredentials table
    Set ws = ThisWorkbook.Sheets("UserCredentials")
    Set tbl = ws.ListObjects("UserCredentials")
    ' Get the username from input field
    username = Trim(Me.txtUsername.value)
    ' Validate input
    If username = "" Then
        Me.lblMessage.Caption = "Enter a username to delete."
        Exit Sub
    End If
    ' Get column index for "USERNAME"
    colUsername = tbl.ListColumns("USERNAME").Index
    ' Search for the username in UserCredentials table
    Set foundUser = tbl.ListColumns("USERNAME").DataBodyRange.Find(What:=username, LookAt:=xlWhole)
    ' If user is found, delete the row
    If Not foundUser Is Nothing Then
        tbl.ListRows(foundUser.row - tbl.DataBodyRange.row + 1).Delete
        Me.lblMessage.Caption = "User deleted successfully."
    Else
        Me.lblMessage.Caption = "User not found."
    End If
End Sub

