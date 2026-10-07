using System.Collections.Generic;
using UnityEngine;

namespace Glassfall.Gameplay.Surface
{
    [DisallowMultipleComponent]
    public sealed class DropletSurfaceSensor : MonoBehaviour
    {
        private readonly List<SurfaceZone> _activeZones =
            new List<SurfaceZone>(2);

        private SurfaceZone _activeZone;

        public SurfaceType CurrentSurface => _activeZone != null
            ? _activeZone.SurfaceType
            : SurfaceType.NormalGlass;

        public SurfaceModifiers CurrentModifiers => _activeZone != null
            ? _activeZone.Modifiers
            : SurfaceModifiers.Normal;

        public void ResetState()
        {
            _activeZones.Clear();
            _activeZone = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            SurfaceZone zone = other.GetComponentInParent<SurfaceZone>();
            if (zone == null)
                return;

            _activeZones.Remove(zone);
            _activeZones.Add(zone);
            RefreshActiveZone();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            SurfaceZone zone = other.GetComponentInParent<SurfaceZone>();
            if (zone == null)
                return;

            _activeZones.Remove(zone);
            RefreshActiveZone();
        }

        private void RefreshActiveZone()
        {
            for (int i = _activeZones.Count - 1; i >= 0; i--)
            {
                SurfaceZone zone = _activeZones[i];
                if (zone != null && zone.isActiveAndEnabled)
                {
                    _activeZone = zone;
                    return;
                }

                _activeZones.RemoveAt(i);
            }

            _activeZone = null;
        }
    }
}
