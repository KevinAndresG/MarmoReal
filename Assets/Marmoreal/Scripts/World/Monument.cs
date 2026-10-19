using System;
using UnityEngine;

namespace Marmoreal
{
    /// <summary>El Gran Monumento físico: cada hijo de `pieces` (orden de campaña: Base, Columnas, Torso…)
    /// se activa al conseguir su pieza. Las piezas se editan a mano en la escena (--- MONUMENTO ---).</summary>
    public class Monument : MonoBehaviour
    {
        [Tooltip("Piezas en orden de campaña; índice = campaignStage de la sala que la da")]
        public GameObject[] pieces;
        [NonSerialized] int shownMask = -1;

        void Update()
        {
            var gm = GameManager.I;
            if (!gm || pieces == null) return;
            int mask = 0;
            foreach (var p in gm.S.pieces) if (p >= 0 && p < 31) mask |= 1 << p;
            if (mask == shownMask) return;
            shownMask = mask;
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i]) pieces[i].SetActive((mask >> i & 1) != 0);
        }
    }
}
