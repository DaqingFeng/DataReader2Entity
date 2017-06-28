

# DataReader2Entity
C# IDataReader to Entity . Support  multiple thread .Nullable property convert.

 
## Usage

 ```c#
  
       var sdr = baseDao.ExecuteReader(connectionString,cmdType,cmdText,commandParameters){
       IList<BillCategoryQueryResponse> lst = DataReaderConverter.Fill<BillCategoryQueryResponse>(sdr);
       
        var sdrtwo = baseDao.ExecuteReader(connectionString,cmdType,cmdTextByPrimaryKey,commandParameters){
        BillCategoryQueryResponse  entity = DataReaderConverter.FillSingle<BillCategoryQueryResponse>(sdrtwo);
        
 ```
