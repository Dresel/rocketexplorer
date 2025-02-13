using Nethereum.RPC.Eth.DTOs;

namespace RocketExplorer.Ethereum;

public class Helper
{
	public static async Task<ulong?> FindFirstBlock(
		Func<BlockParameter, Task<bool>> smartContractCall, ulong initialBlock, ulong latestBlock,
		uint blockIncrement = 150)
	{
		ulong currentBlock = initialBlock;
		ulong lastFalse = initialBlock;
		ulong firstTrue = 0;

		while (currentBlock < latestBlock)
		{
			if (await smartContractCall(new BlockParameter(currentBlock)))
			{
				firstTrue = currentBlock;
				break;
			}

			lastFalse = currentBlock;
			currentBlock = Math.Min(latestBlock, currentBlock + blockIncrement);
		}

		if (firstTrue == 0)
		{
			return null;
		}

		while (lastFalse + 1 < firstTrue)
		{
			ulong middleBlock = (lastFalse + firstTrue) / 2;

			if (await smartContractCall(new BlockParameter(middleBlock)))
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