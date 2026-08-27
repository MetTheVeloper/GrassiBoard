using GrassiBoard.Services;

if (NativeAudioEngine.ExpectedApiVersion != 11U)
{
    Console.Error.WriteLine($"Looper local smoke expected ABI 11, got {NativeAudioEngine.ExpectedApiVersion}.");
    return 1;
}

Console.WriteLine("GrassiLooper local ModuleInitializer smoke tests passed (Gate 1 + Gate 2 + Gate 3, ABI 11).");
return 0;
