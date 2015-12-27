// -------------------------------------
//  Domain		: Avariceonline.com
//  Author		: Nicholas Ventimiglia
//  Product		: Unity3d Foundation
//  Published		: 2015
//  -------------------------------------
using UnityEngine;

namespace Foundation.Terminal.Internal
{
    /// <summary>
    /// Extends the console with a 'clear' command
    /// </summary>
    [AddComponentMenu("Foundation/Terminal/ClearCommand")]
    public class ClearCommand : MonoBehaviour
    {
        protected void Awake()
        {
            TerminalModel.Add(new TerminalCommand
            {
                Label = "Clear",
                Method = () => TerminalModel.Clear()
            });
        }
    }
}
