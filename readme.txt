The provided project is a Windows Forms semi completed TCP Sockets Chat project. 

How To Run the Project
========================
To run it you will need to run at least 2 instances of the project exe.
Host Server:
If you open the project in Visual Studio and run it, choose to Host Server. 
Host Client(s):
In the project folder, navigate to Windows Forms core chat\bin\Debug\netcoreapp3.1 folder and run Windows Forms core chat.exe and click Join Server. 

Clients should be able to type messages to the server and the server can broadcast those messages to all clients.

The Project
============
*** COMMANDS ***
!username: let user get username when they join the server.
!user: change username.
!who: shows who are connecting to the server.
!about: shows information of application.
!timestamps: on/off time with messages.
!whisper: send private messages to an user.
!mod: promote/demote an user as a 'mod' role.
!mods: list of mod in the server.
!kick: disconnect an user as a mod.