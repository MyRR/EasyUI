﻿using System;

namespace Zephyr.Data
{
	internal abstract class BaseUpdateBuilder
	{
		public BuilderData Data { get; set; }
		protected ActionsHandler Actions { get; set; }

		public BaseUpdateBuilder(IDbProvider provider, IDbCommand command, string name)
		{
			Data =  new BuilderData(command, name);
			Actions = new ActionsHandler(Data);
		}

		public int Execute()
		{
            //mod by liuhuisheng start
            //if (Data.Columns.Count == 0
            //    || Data.Where.Count == 0)
            //    throw new FluentDataException("Columns or where filter have not yet been added.");
            if (Data.Columns.Count == 0
                || (Data.Where.Count == 0 && Data.WhereSql.Length == 0))
                throw new FluentDataException("Columns or where filter have not yet been added.");
            //mod by liuhuisheng end

			Data.Command.ClearSql.Sql(Data.Command.Data.Context.Data.Provider.GetSqlForUpdateBuilder(Data));
		
			return Data.Command.Execute();
		}
	}
}
