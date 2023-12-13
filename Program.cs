using System.Threading.Channels;
using MathNet.Numerics.Distributions;

namespace BankQueueingSystem;

public class Client
{
	public int Id { get; set; }
	public DateTime ArrivalTime { get; set; }
	public TimeSpan ServiceTime { get; set; }
	public bool IsUrgent { get; set; }
}

public class Program
{
	private static int _urgent;
	private static int _regular;

	#region Data for server 1
	private static int _processedUrgentS1;
	private static int _processedRegularS1;

	private static int _declinedUrgentS1;
	private static int _declinedRegularS1;
	#endregion

	#region Data for server 2
	private static int _processedUrgentS2;
	private static int _processedRegularS2;

	private static int _declinedUrgentS2;
	private static int _declinedRegularS2;
	#endregion

	#region Data for server 3
	private static int _processedUrgentS3;
	private static int _processedRegularS3;

	private static int _declinedUrgentS3;
	private static int _declinedRegularS3;
	#endregion

	private static readonly Channel<Client> ClientChannel = Channel.CreateUnbounded<Client>();

	private static readonly Random RegularRandom = new(12345);
	private static Exponential _regularExponential;
	private static Exponential _urgentExponential;
	private static readonly Random UrgentRandom = new(54321);

	private static int _clientCreationDelay;

	private static DateTime _programStartedDateTime;

	private static readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

	private static async Task Main()
	{
		#region Mass-processing system input params

		Console.WriteLine("(Time is in seconds)");
		Console.Write("Enter simulation time: ");
		var simulationTimeSeconds = TimeSpan.FromSeconds(int.Parse(Console.ReadLine()));

		_regularExponential = new Exponential(5, new Random(12345));
		_urgentExponential = new Exponential(2, new Random(54321));

		Console.Write("\nEnter regular clients percentage: ");
		var regularPercentage = double.Parse(Console.ReadLine());
		Console.Write("Enter urgent clients percentage: ");
		var urgentPercentage = double.Parse(Console.ReadLine());

		Console.Write("\nEnter max wait time for regular clients: ");
		var maxWaitTimeForRegular = TimeSpan.FromSeconds(int.Parse(Console.ReadLine()));
		Console.Write("Enter max wait time for urgent clients: ");
		var maxWaitTimeForUrgent = TimeSpan.FromSeconds(int.Parse(Console.ReadLine()));

		Console.Write("\nEnter delay between client creation: ");
		_clientCreationDelay = int.Parse(Console.ReadLine()) * 1000;

		Console.Write("\nEnter number of processing servers: ");
		var numberOfServers = int.Parse(Console.ReadLine());

		Console.WriteLine("\n\n");

		#endregion

		CancellationTokenSource cts = new(simulationTimeSeconds);

		_programStartedDateTime = DateTime.Now;
		var taskList = new List<Task>
		{
			// Creating urgent and regular clients generators
			RegularClientsGenerator(regularPercentage, cts.Token),
			UrgentClientsGenerator(urgentPercentage, cts.Token),
			// Adding processing task 
			ProcessQueueS1(maxWaitTimeForRegular, maxWaitTimeForUrgent, cts.Token)
		};

		// Applying number of desired servers to process queue
		switch (numberOfServers)
		{
			case 2:
				taskList.Add(ProcessQueueS2(maxWaitTimeForRegular, maxWaitTimeForUrgent, cts.Token));
				break;
			case 3:
				taskList.Add(ProcessQueueS2(maxWaitTimeForRegular, maxWaitTimeForUrgent, cts.Token));
				taskList.Add(ProcessQueueS3(maxWaitTimeForRegular, maxWaitTimeForUrgent, cts.Token));
				break;
		} 

		try
		{
			await Task.WhenAll(taskList);
		}
		finally
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"\n\n--- GENERATED COUNT ---" +
							  $"\nTotal: {_regular + _urgent}" +
							  $"\nRegular: {_regular}" +
							  $"\nUrgent: {_urgent}");
			Console.ResetColor();

			var dataForSecondProcessed = $"\n\nTotal S2: {_processedRegularS2 + _processedUrgentS2}" +
			                            $"\nRegular S2: {_processedRegularS2}" +
			                            $"\nUrgent S2: {_processedUrgentS2}";

			var dataForThirdProcessed = $"\n\nTotal S3: {_processedRegularS3 + _processedUrgentS3}" +
			                           $"\nRegular S3: {_processedRegularS3}" +
			                           $"\nUrgent S3: {_processedUrgentS3}";

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.WriteLine($"\n\n--- PROCESSED COUNT ---" +
							  $"\nTotal S1: {_processedRegularS1 + _processedUrgentS1}" +
							  $"\nRegular S1: {_processedRegularS1}" +
							  $"\nUrgent S1: {_processedUrgentS1}" +
							  numberOfServers switch
							  {
								  2 => dataForSecondProcessed,
								  3 => dataForSecondProcessed + dataForThirdProcessed,
								  _ => ""
							  });
			Console.ResetColor();

			var dataForSecondDeclined = $"\n\nTotal S2: {_declinedRegularS2 + _declinedUrgentS2}" +
			                    $"\nRegular S2: {_declinedRegularS2}" +
			                    $"\nUrgent S2: {_declinedUrgentS2}";

			var dataForThirdDeclined = $"\n\nTotal S3: {_declinedRegularS3 + _declinedUrgentS3}" +
			                    $"\nRegular S3: {_declinedRegularS3}" +
			                    $"\nUrgent S3: {_declinedUrgentS3}";

			Console.ForegroundColor = ConsoleColor.DarkRed;
			Console.WriteLine($"\n\n--- DECLINED COUNT ---" +
							  $"\nTotal S1: {_declinedRegularS1 + _declinedUrgentS1}" +
							  $"\nRegular S1: {_declinedRegularS1}" +
							  $"\nUrgent S1: {_declinedUrgentS1}" + 
							  numberOfServers switch
							  {
								  2 => dataForSecondDeclined,
								  3 => dataForSecondDeclined + dataForThirdDeclined,
								  _ => ""
							  });
			Console.ResetColor();

			Console.ReadKey();
		}
	}

	private static async Task RegularClientsGenerator(double regularClientsPercentage, CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
			try
			{
				if (_regularExponential.Sample() > regularClientsPercentage)
				{
					await Task.Delay(_clientCreationDelay, ct);
					continue;
				}

				await _semaphoreSlim.WaitAsync(ct);
				if (ct.IsCancellationRequested)
					break;

				Interlocked.Increment(ref _regular);
				var client = new Client
				{
					Id = _regular,
					ArrivalTime = DateTime.Now,
					IsUrgent = false,
					ServiceTime = TimeSpan.FromSeconds(RegularRandom.Next(1, 5))
				};

				Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Created regular. ID: {client.Id}. ArrivalTime: {client.ArrivalTime}. ServiceTime: {client.ServiceTime}");
				await ClientChannel.Writer.WriteAsync(client, ct);
				_semaphoreSlim.Release();

				await Task.Delay(_clientCreationDelay, ct);
			}
			catch
			{
				return;
			}

		ClientChannel.Writer.Complete();
	}

	private static async Task UrgentClientsGenerator(double urgentClientsPercentage, CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
			try
			{
				if (_urgentExponential.Sample() > urgentClientsPercentage)
				{
					await Task.Delay(_clientCreationDelay, ct);
					continue;
				}

				await _semaphoreSlim.WaitAsync(ct);
				if (ct.IsCancellationRequested)
					break;

				Interlocked.Increment(ref _urgent);
				var client = new Client
				{
					Id = _urgent,
					ArrivalTime = DateTime.Now,
					IsUrgent = true,
					ServiceTime = TimeSpan.FromSeconds(UrgentRandom.Next(1, 5))
				};

				Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Created urgent. ID: {client.Id}. ArrivalTime: {client.ArrivalTime}. ServiceTime: {client.ServiceTime}");
				await ClientChannel.Writer.WriteAsync(client, ct);
				_semaphoreSlim.Release();

				await Task.Delay(_clientCreationDelay, ct);
			}
			catch
			{
				return;
			}

		ClientChannel.Writer.Complete();
	}

	private static async Task ProcessQueueS1(TimeSpan maxWaitTimeForRegular, TimeSpan maxWaitTimeForUrgent, CancellationToken ct)
	{
		try
		{
			await Parallel.ForEachAsync(ClientChannel.Reader.ReadAllAsync(ct), new ParallelOptions
			{
				MaxDegreeOfParallelism = 1,
				CancellationToken = ct
			},
				async (client, token) =>
				{
					if (DateTime.Now - client.ArrivalTime >
						(client.IsUrgent ? maxWaitTimeForUrgent : maxWaitTimeForRegular))
					{
						if (client.IsUrgent)
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedUrgentS1);
						}
						else
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedRegularS1);
						}

						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Declined S1 " +
										  (client.IsUrgent
											  ? "urgent "
											  : "regular ") +
										  $"ID: {client.Id}");
						Console.ResetColor();
						return;
					}

					await Task.Delay(client.ServiceTime, token);
					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Processed S1 " +
									  (client.IsUrgent
										  ? "urgent "
										  : "regular ") +
									  $"ID: {client.Id}");
					Console.ResetColor();

					if (client.IsUrgent)
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedUrgentS1);
					}
					else
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedRegularS1);
					}
				});
		}
		catch
		{
		}
	}

	private static async Task ProcessQueueS2(TimeSpan maxWaitTimeForRegular, TimeSpan maxWaitTimeForUrgent, CancellationToken ct)
	{
		try
		{
			await Parallel.ForEachAsync(ClientChannel.Reader.ReadAllAsync(ct), new ParallelOptions
				{
					MaxDegreeOfParallelism = 1,
					CancellationToken = ct
				},
				async (client, token) =>
				{
					if (DateTime.Now - client.ArrivalTime >
					    (client.IsUrgent ? maxWaitTimeForUrgent : maxWaitTimeForRegular))
					{
						if (client.IsUrgent)
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedUrgentS2);
						}
						else
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedRegularS2);
						}

						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Declined S2 " +
						                  (client.IsUrgent
							                  ? "urgent "
							                  : "regular ") +
						                  $"ID: {client.Id}");
						Console.ResetColor();
						return;
					}

					await Task.Delay(client.ServiceTime, token);
					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Processed S2 " +
					                  (client.IsUrgent
						                  ? "urgent "
						                  : "regular ") +
					                  $"ID: {client.Id}");
					Console.ResetColor();

					if (client.IsUrgent)
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedUrgentS2);
					}
					else
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedRegularS2);
					}
				});
		}
		catch
		{
		}
	}

	private static async Task ProcessQueueS3(TimeSpan maxWaitTimeForRegular, TimeSpan maxWaitTimeForUrgent, CancellationToken ct)
	{
		try
		{
			await Parallel.ForEachAsync(ClientChannel.Reader.ReadAllAsync(ct), new ParallelOptions
				{
					MaxDegreeOfParallelism = 1,
					CancellationToken = ct
				},
				async (client, token) =>
				{
					if (DateTime.Now - client.ArrivalTime >
					    (client.IsUrgent ? maxWaitTimeForUrgent : maxWaitTimeForRegular))
					{
						if (client.IsUrgent)
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedUrgentS3);
						}
						else
						{
							if (token.IsCancellationRequested)
								return;
							Interlocked.Increment(ref _declinedRegularS3);
						}

						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Declined S3 " +
						                  (client.IsUrgent
							                  ? "urgent "
							                  : "regular ") +
						                  $"ID: {client.Id}");
						Console.ResetColor();
						return;
					}

					await Task.Delay(client.ServiceTime, token);
					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine($"{DateTime.Now - _programStartedDateTime} | Processed S3 " +
					                  (client.IsUrgent
						                  ? "urgent "
						                  : "regular ") +
					                  $"ID: {client.Id}");
					Console.ResetColor();

					if (client.IsUrgent)
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedUrgentS3);
					}
					else
					{
						if (token.IsCancellationRequested)
							return;
						Interlocked.Increment(ref _processedRegularS3);
					}
				});
		}
		catch
		{
		}
	}
}