# Selected Item Status Bar
The user workflow depends on selecting a dataserver, then a database, 
and then an object such as table, view, stored proc, or function.
These 3 selections form a selection hierarchy:  Server => Database => Object
These 3 selection values should be maintained in session variables for the duration of the session.

Instead of having individual pages show the selected values near the top of the page, 
let's create a small component that is a narrow horizontal strip which is included 
in every page that displays object (table, sproc, func, view) information.
It is formated thusly:
Server: ssssssssssss      Database: dddddddddd   Table:  tttttttttt
or 
Server: ssssssssssss      Database: dddddddddd   SProc:  pppppppppp
or
Server: ssssssssssss      Database: dddddddddd   Func:   ffffffffff
or
Server: ssssssssssss      Database: dddddddddd   View:   vvvvvvvvvv

On utility pages that don't show object details, 
Server: ssssssssssss      Database: dddddddddd

On reports selection pages,
Server: ssssssssssss      Database: dddddddddd

