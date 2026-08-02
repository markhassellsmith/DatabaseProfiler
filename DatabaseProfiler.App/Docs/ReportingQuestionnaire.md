# Reporting Feature Questionnaire

## 1. Purpose
What is the primary purpose of the reporting feature?

- [2] Documentation
- [1] Analysis
- [ ] Export/share
- [ ] Audit/history
- [ ] Other: 

Comments:


## 2. Audience
Who is the reporting feature for?

- [3] Developers
- [2] Analysts
- [1] DBAs
- [ ] Managers
- [ ] External users
- [ ] Other: 

Comments:


## 3. Scope
What should the reporting feature cover?

- [ ] One table
- [1] Selected tables
- [ ] One database
- [ ] One server
- [ ] Entire discovered set
- [2] Other: 

Comments:
A list of tables and relationship-focused report on the database that identifies actual relationships and likely relationships not stored in the database. 

A table-focused report in Excel that can be generated for a single table or multiple tables, including all relevant details and profiling information.	
The column schema and the profiling data can be included in the same display grid, but the other items (indexes, keys) should be in separate sections.  The report should be able to be generated for a single table or multiple tables, and should include all relevant details and profiling information.

A script-focused report for other types of objects (views, functions, stored procedures) that includes the object script and any relevant details.  The report should be able to be generated for a single object or multiple objects, and should include all relevant details.

## 4. Content
What should be included?  That depends on which of the three report types is selected.  

For the list of tables and relationships, the following should be included:
Count and List of tables in alphabetical order, with the following details for each table:
Number of rows. Primary Key.  Foreign Keys. Implicit foreign keys.

For table-focused reports, the following items should be included:
The column attributes and the data profile for each column will be combined into a single info grid.
Give the column attributes first, then the data profile info for the values in the column.  
The column attributes should include all of the info on the Column Browser, including the column name, data type, length, precision, scale, nullability, default value, and any other relevant attributes..
The data profile values should include all of the info on the Data Profile page, including the row count, distinct count, null count, min, max, average, and standard deviation.  
The data profile values should be calculated for the entire table, not just the first 1000 rows..
Maintain the same order of information as the Column Browser and Data Profile pages, 
so that users can easily compare the information they are looking for.

A script -focused report for other types of objects (views, functions, stored procedures) 
should be in a format that can be imported into a text editor or IDE, 
and should include the object script and any relevant details.  
The report should be able to be generated for a single object or multiple objects, 
and should include all relevant details.

## 5. Output format
What format(s) should the reporting feature support?
Depends on whether the content is tabular or script-focused.  
Tabular reports should be able to be exported to Excel, CSV, and PDF. 
Script-focused reports should be able to be exported to text files.

- [ ] HTML
- [x] PDF
- [x] Excel
- [x] CSV
- [x] Markdown
- [ ] JSON
- [ ] Other: 

Comments:
Markdown is include for use with AI agents in the future.  JSON is not needed at this time.


## 6. Presentation style
How should the reports look?

- [ ] Simple
- [x] Detailed
- [ ] Print-friendly
- [ ] Branded
- [ ] Interactive
- [x] Static
- [ ] Other: 

Comments:
Printing should be possible with scripts or Excel, but not necessarily with the PDF output.  The PDF output should be more of a static report, while the Excel output should be more interactive and allow for filtering and sorting of the data.  The Markdown output should be simple and easy to read, while the CSV output should be simple and easy to import into other tools.


## 7. Navigation
How should users access it?

- [x] Top navigation reporting features page
- [ ] From Object Browser
- [ ] From Data Profile page
- [ ] From Column Browser
- [ ] Direct export action
- [ ] Other: 

Comments:
The reporting interface will be accessed from the top navigation reporting features page, and will allow users to select the objects they want to include in the report.  The report will be generated based on the selected objects, and will include all relevant details and profiling information.
The selected server and selected database should be used to list the database objects for reporting.

For reports on tables, the system will generate an Excel file with one table per sheet.
Summary data will appear at the top of the sheet, with the column attributes and data profile information for each column in the table below.  
The report will be generated based on the selected objects, and will include all relevant details and profiling information.
The sheets will be named after the table name, arranged in alphabetical order, and will include all relevant details and profiling information for each column in the table.

For reports on scripted objects, the system will generate a text file with one object per section.

## 8. Filtering and selection
What controls should shape the reporting feature?

- [ ] Current server
- [ ] Current database
- [ ] Current selected object
- [ ] Table only
- [ ] View only
- [ ] Function only
- [ ] Stored procedure only
- [ ] Other: 

Comments:
Given server and database selection, the system will list all objects in the database for selection.  
The user can select one or more objects to include in the report.  
The report will be generated based on the selected objects, and will include all relevant details and profiling information.
One or more files may be created based on the type of the report.  Server name, database name, and date-time will preface the report.


## 9. reporting feature structure
How should the reporting feature be organized?

- [x] Single page summary
- [x] Sectioned reporting feature
- [x] Per-table sections
- [ ] Expandable sections
- [ ] Appendix-style output
- [ ] Other: 

Comments:
Excel will have leading summary sheet, then one sheet per object.

## 10. Empty / missing data behavior
How should missing data be handled?

- [ ] Hide section
- [ ] Show placeholder
- [ ] Show warning
- [x] Show zero / not available
- [ ] Other: 

Comments:
Data profiling should be able to identify and count nulls and empty strings.



## 11. Persistence
Should reporting features be saved or generated on demand?

- [ ] On demand only
- [ ] Cached
- [ ] Saved to file
- [ ] Snapshotted
- [ ] Other: 

Comments:
Not sure what this means.  No need to persist any choices.

## 12. Theme and styling
Should the reporting feature follow the app theme?

- [ ] Yes
- [x] No
- [ ] Print-friendly theme
- [ ] Separate reporting feature styling
- [ ] Other: 

Comments:
Generic Excel and Text documents.

## 13. Priority items
List the top 3 must-haves.

1. On the report generation web page, pick-lists of objects should appear in collapsible panels, just like the Object Browser page.  Use checkboxes to allow multiple objects for reporting.
2. In table reports, table column attributes and data profile information for each column should be combined in a single grid, as mentioned above.
3. Use two separate color shades (light background colors) in table reports to distinguish the column attribute info versus the data profile info.


Comments:


## 14. Nice-to-haves
List optional features.

1. 
2. 
3. 

Comments:


## 15. Notes
Any other requirements or constraints?

Comments: