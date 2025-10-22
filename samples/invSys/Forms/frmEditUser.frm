VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmEditUser 
   Caption         =   "Edit User"
   ClientHeight    =   3660
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   4560
   OleObjectBlob   =   "frmEditUser.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmEditUser"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub UserForm_Initialize()
    ' Load usernames into cmbUserName
    modUserAuth.LoadUsersIntoComboBox Me.cmbUserName
    ' Load roles into cmbRoleChange
    modUserAuth.LoadRolesIntoComboBox Me.cmbRoleChange
End Sub
Private Sub btnUpdateUser_Click()
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundUser As Range
    Dim selectedUsername As String
    Dim newUsername As String
    Dim newPIN As String
    Dim newRole As String
    Dim colUsername As Integer, colPIN As Integer, colRole As Integer
    ' Set reference to UserCredentials table
    Set ws = ThisWorkbook.Sheets("UserCredentials")
    Set tbl = ws.ListObjects("UserCredentials")
    ' Get user input
    selectedUsername = Trim(Me.cmbUserName.value)
    newUsername = Trim(Me.txtNewUserName.value)
    newPIN = Trim(Me.txtNewPIN.value)
    newRole = Trim(Me.cmbRoleChange.value)
    ' Validate selection
    If selectedUsername = "" Then
        Me.lblMessages.Caption = "Select a user to update."
        Exit Sub
    End If
    ' Get column indexes dynamically
    colUsername = tbl.ListColumns("USERNAME").Index
    colPIN = tbl.ListColumns("PIN").Index
    colRole = tbl.ListColumns("ROLE").Index
    ' Find user in UserCredentials table
    Set foundUser = tbl.ListColumns("USERNAME").DataBodyRange.Find(What:=selectedUsername, LookAt:=xlWhole)
    ' If user is not found, exit
    If foundUser Is Nothing Then
        Me.lblMessages.Caption = "User not found."
        Exit Sub
    End If
    ' Update username if a new one is provided and is different from current
    If newUsername <> "" And newUsername <> selectedUsername Then
        Dim checkUsername As Range
        Set checkUsername = tbl.ListColumns("USERNAME").DataBodyRange.Find(What:=newUsername, LookAt:=xlWhole)
        If Not checkUsername Is Nothing Then
            Me.lblMessages.Caption = "Username already in use."
            Exit Sub
        End If
        foundUser.value = newUsername
    End If
    ' Update PIN if a new one is provided
    If newPIN <> "" Then
        If Not IsNumeric(newPIN) Or Len(newPIN) <> 6 Then
            Me.lblMessages.Caption = "PIN must be exactly 6 digits."
            Exit Sub
        End If
        foundUser.offset(0, colPIN - colUsername).value = newPIN
    End If
    ' Update role if a new one is selected
    If newRole <> "" Then
        foundUser.offset(0, colRole - colUsername).value = newRole
    End If
    ' Confirmation message
    Me.lblMessages.Caption = "User updated successfully."
End Sub
Private Sub btnNewPIN_Click()
    Dim randomPIN As String
    ' Generate a random 6-digit number
    randomPIN = Format(Int((900000 * Rnd) + 100000), "000000")
    ' Display the generated PIN in txtPIN
    Me.txtNewPIN.value = randomPIN
End Sub

