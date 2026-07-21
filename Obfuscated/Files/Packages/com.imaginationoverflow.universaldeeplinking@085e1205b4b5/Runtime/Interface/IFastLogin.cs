using DragonPlus.Core;

namespace DragonPlus.DeepLinking
{
    public interface IFastLogin : IAutoResolvable
    {
        void Initialize();
        void JumpBack(string url, string avatarId, string playerName);
    }
}