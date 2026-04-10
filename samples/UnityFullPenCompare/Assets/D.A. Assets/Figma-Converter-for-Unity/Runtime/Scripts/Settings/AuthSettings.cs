using DA_Assets.FCU.Model;
using System;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class AuthSettings : FcuBase
    {
        // Field initializers use FcuConfig constants (no Instance required).
        [SerializeField] string clientId     = FcuConfig.DefaultClientId;
        [SerializeField] string clientSecret = FcuConfig.DefaultClientSecret;
        [SerializeField] string redirectUri  = FcuConfig.DefaultRedirectUri;
        [SerializeField] FigmaScope scopes   = FcuConfig.DefaultScopes;

        public string     ClientId     { get => clientId;     set => clientId = value; }
        public string     ClientSecret { get => clientSecret; set => clientSecret = value; }
        public string     RedirectUri  { get => redirectUri;  set => redirectUri = value; }
        public FigmaScope Scopes       { get => scopes;       set => scopes = value; }
    }
}
