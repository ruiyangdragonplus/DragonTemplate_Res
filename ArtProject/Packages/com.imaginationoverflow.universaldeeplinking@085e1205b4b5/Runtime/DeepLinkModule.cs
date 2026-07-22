using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DragonPlus.Core;

namespace DragonPlus.DeepLinking
{
    public class DeepLinkModule : SDKModule
    {
        private FastLoginImpl   _fastLoginImpl;
        private DeepLinkingImpl _deepLinkingImpl;

        public override Task DoInitializeOnLaunch(List<Type> needInstallDependencies = null)
        {
            _fastLoginImpl = new FastLoginImpl();
            _fastLoginImpl.Initialize();

            _deepLinkingImpl = new DeepLinkingImpl();
            _deepLinkingImpl.Initialize();

            SDK.Instance.InstallDependency(_fastLoginImpl);
            SDK.Instance.InstallDependency(_deepLinkingImpl);

            return Task.CompletedTask;
        }
    }
}