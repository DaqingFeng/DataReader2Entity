

# DataReader2Entity
C# IDataReader to Entity . Support  multiple thread .Nullable property convert.

 
## Usage

      var result = baseDao.ExecuteReader(connectionString,cmdType,cmdText,commandParameters){
          IList<BillCategoryQueryResponse> lst = DataReaderConverter.Fill<BillCategoryQueryResponse>(sdr);
          return lst;
      });
