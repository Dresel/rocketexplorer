using Nethereum.RPC.Eth.DTOs;

namespace RocketExplorer.Ethereum;

public class Helper
{
	public static async Task<long?> FindFirstBlockAsync(
		Func<BlockParameter, Task<bool>> smartContractCall, long initialBlock, long latestBlock,
		uint blockIncrement = 150)
	{
		long currentBlock = initialBlock;
		long lastFalse = initialBlock;
		long firstTrue = 0;

		while (currentBlock <= latestBlock)
		{
			if (await smartContractCall(new BlockParameter((ulong)currentBlock)))
			{
				firstTrue = currentBlock;
				break;
			}

			lastFalse = currentBlock;

			if (currentBlock == latestBlock)
			{
				break;
			}

			currentBlock = Math.Min(latestBlock, currentBlock + blockIncrement);
		}

		if (firstTrue == 0)
		{
			return null;
		}

		while (lastFalse + 1 < firstTrue)
		{
			long middleBlock = (lastFalse + firstTrue) / 2;

			if (await smartContractCall(new BlockParameter((ulong)middleBlock)))
			{
				firstTrue = middleBlock;
			}
			else
			{
				lastFalse = middleBlock;
			}
		}

		return firstTrue;
	}
}