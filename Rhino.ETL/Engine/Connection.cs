using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Threading;
using Boo.Lang;
using log4net;

namespace Rhino.ETL
{
	public class Connection
	{
		private readonly string name;
		private string connectionString;
		private string connectionStringName;
		private int concurrentConnections = 5;
		private Type connectionType;
		private ICallable connectionStringGenerator;
		private static ILog logger = LogManager.GetLogger(typeof (Connection));

		private Semaphore connectionSejoinhore;

		private Semaphore ConnectionSejoinhore
		{
			get
			{
				if(connectionSejoinhore==null)
				{
					lock(this)
					{
						if(connectionSejoinhore==null)
						{
							connectionSejoinhore = new Semaphore(0, ConcurrentConnections);
							connectionSejoinhore.Release(ConcurrentConnections);
						}
					}
				}
				return connectionSejoinhore;
			}
		}

		public Connection(string name)
		{
			this.name = name;
			EtlConfigurationContext.Current.AddConnection(name, this);
		}


		public ICallable ConnectionStringGenerator
		{
			get { return connectionStringGenerator; }
			set { connectionStringGenerator = value; }
		}

		public Type ConnectionType
		{
			get { return connectionType; }
			set { connectionType = value; }
		}

		public string Name
		{
			get { return name; }
		}

		public string ConnectionString
		{
			get
			{
				if (ConnectionStringGenerator!=null)
				{
					return (string)ConnectionStringGenerator.Call(new object[0]);
				}
				if(string.IsNullOrEmpty(ConnectionStringName)==false)
				{
					ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionStringName];
					if(settings==null)
					{
						throw new ConfigurationErrorsException(string.Format("[Connection: {1}] Named connection string '{0}' does not exists", ConnectionStringName, Name));
					}
					return settings.ConnectionString;
				}
				return connectionString;
			}
			set { connectionString = value; }
		}

		public string ConnectionStringName
		{
			get { return connectionStringName; }
			set { connectionStringName = value; }
		}

		public int ConcurrentConnections
		{
			get { return concurrentConnections; }
			set { concurrentConnections = value; }
		}

		public IDbConnection TryAcquire()
		{
			bool aquired = ConnectionSejoinhore.WaitOne(0,false);
			if(aquired==false)
				return null;
			try
			{
				return CreateConnection();
			}
			catch (Exception e)
			{
				logger.Error("Failed to acquire connection in "+Name, e);
				throw;
			}
		}

		private IDbConnection CreateConnection()
		{
			if (ConnectionType == null)
				throw new ArgumentNullException("ConnectionType", "ConnectionType must be set to a value");
			IDbConnection connection = (IDbConnection)Activator.CreateInstance(ConnectionType);
			connection.ConnectionString = ConnectionString;
			connection.Open();
			return connection;
		}

		public void Release(IDbConnection connection)
		{
			try
			{
				if (connection != null)
					connection.Dispose();
			}
			catch (Exception e)
			{
				logger.Error("Exception when disposing of connection in "+name, e);
			}
			ConnectionSejoinhore.Release();
		}
	}

	/// <summary>
	/// this is the easiest way to support both Connection and connection in the DSL :-)
	/// </summary>
	public class connection : Connection
	{
		public connection(string name) : base(name)
		{
		}
	}
}
