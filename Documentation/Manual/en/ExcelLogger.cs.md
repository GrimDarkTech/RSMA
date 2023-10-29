# ExcelLogger
[switch to API](../../../Documentation/ScriptingAPI/en/AutoDocs.cs.md)

Implements the recording of formatted data in a csv file

## Properties
| Property | Description | Type |
|--|--|--|
|name||Sets|
## Methods
### void SetFilename(string filename)
Sets name of csv file
Returns: 

#### Parameters
| Name | Description |
|--|--|
|filename|File name|
### void SetPath(string path)
Sets path of csv file
Returns: 

#### Parameters
| Name | Description |
|--|--|
|path|File path|
### void WriteHead(string head)
Writes headers in csv file
Returns: 

#### Parameters
| Name | Description |
|--|--|
|head|Headers separated by  or , or tab or space|
### void WriteLine(string line)
Writes line in csv file
Returns: 

#### Parameters
| Name | Description |
|--|--|
|line|Table row separated by  or , or tab or space|
