using MessagePack;
using Microsoft.AspNetCore.Components;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Shared;

namespace RocketExplorer.Web.Components;

public partial class BlockInfo : ComponentBase, IDisposable
{
	private readonly CancellationTokenSource cancellationTokenSourceTimer = new();
	private bool isDisposed;

	private PeriodicTimer? timer;

	[Inject]
	public AppState AppState { get; set; } = null!;

	[Inject]
	public Configuration Configuration { get; set; } = null!;

	[Inject]
	public HttpClient HttpClient { get; set; } = null!;

	[Inject]
	public Web3 Web3 { get; set; } = null!;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			this.isDisposed = true;

			if (disposing)
			{
				this.cancellationTokenSourceTimer.Cancel();
				this.cancellationTokenSourceTimer.Dispose();
			}
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			await LoadAsync();
			await InvokeAsync(StateHasChanged);

			this.timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
			_ = UpdateBlockInfo(this.cancellationTokenSourceTimer.Token);
		}
	}

	private async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		Task<BlockWithTransactions>? getBlockTask = Web3.Eth.Blocks
			.GetBlockWithTransactionsByNumber
			.SendRequestAsync(BlockParameter.CreateLatest());
		Task<Stream> getMetadataStreamTask = HttpClient.GetStreamAsync(ObjectStoreMetadataUrl, cancellationToken);

		await Task.WhenAll(getBlockTask, getMetadataStreamTask);

		BlockWithTransactions block = await getBlockTask;
		SnapshotMetadata snapshotMetadata = MessagePackSerializer.Deserialize<SnapshotMetadata>(
			await getMetadataStreamTask, MessagePackSerializerOptions.Standard);

		AppState.Set(block, snapshotMetadata);
	}

	private async Task UpdateBlockInfo(CancellationToken cancellationToken)
	{
		try
		{
			while (!this.cancellationTokenSourceTimer.IsCancellationRequested &&
					await this.timer!.WaitForNextTickAsync(cancellationToken))
			{
				await LoadAsync(cancellationToken);
				await InvokeAsync(StateHasChanged);
			}
		}
		catch (OperationCanceledException)
		{
			// Ignore cancellation
		}
		finally
		{
			this.timer?.Dispose();
		}
	}
}