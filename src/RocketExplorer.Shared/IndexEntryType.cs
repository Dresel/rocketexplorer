namespace RocketExplorer.Shared;

[Flags]
public enum IndexEntryType : short
{
	NodeOperator = 1,

	Megapool = 2,

	MinipoolValidator = 4,

	MegapoolValidator = 8,

	RETHHolder = 16,

	RPLHolder = 32,

	RPLOldHolder = 64,

	RockRETHHolder = 128,

	WithdrawalAddress = 256,

	RPLWithdrawalAddress = 512,

	StakeOnBehalfAddress = 1024,
}