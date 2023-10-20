# ExcelLogger
[switch to API](../../../Documentation/ScriptingAPI/en/ExcelLogger.md)

Implements the recording of formatted data in a csv file

## Properties
| Property | Description | Type |
|--|--|--|
|name||Sets|
## Methods
| Declaration | Description | Returns |
|--|--|--|
|void SetFilename(string filename)|Sets name of csv file||
### Parameters
| Name | Description |
|--|--|
|filename|File name|
|void SetPath(string path)|Sets path of csv file||
### Parameters
| Name | Description |
|--|--|
|path|File path|
|void WriteHead(string head)|Writes headers in csv file||
### Parameters
| Name | Description |
|--|--|
|head|Headers separated by  or , or tab or space|
|void WriteLine(string line)|Writes line in csv file||
### Parameters
| Name | Description |
|--|--|
|line|Table row separated by  or , or tab or space|
