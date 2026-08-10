using System.Collections.Generic;
using UnityEngine;
using RSMA.uDTP.Topics;
using RSMA.MissionPlanner.UI;

namespace RSMA.MissionPlanner.Core
{
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Visual Component")]
        [SerializeField] private MissionWindow _visualComponent;

        [SerializeField] private List<TrajectoryPoint> _points = new List<TrajectoryPoint>();
        public IReadOnlyList<TrajectoryPoint> Points => _points;

        private int _selectedIndex = -1;
        public int SelectedIndex => _selectedIndex;
        public TrajectoryPoint? SelectedPoint => _selectedIndex >= 0 ? _points[_selectedIndex] : (TrajectoryPoint?)null;

        // События для обновления UI и визуализации
        public System.Action OnPointsChanged;
        public System.Action<int> OnPointSelected;
        public System.Action<int> OnPointDeleted;
        public System.Action<TrajectoryPoint> OnPointAdded;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public void AddPoint(Vector3 position, float velocity = 0f)
        {
            if (!IsVisualActive())
            {
                return;
            }

            var point = new TrajectoryPoint
            {
                timestamp = 0,
                position = position,
                targetVelocity = velocity
            };
            _points.Add(point);
            OnPointAdded?.Invoke(point);
            OnPointsChanged?.Invoke();
            SelectPoint(_points.Count - 1);
        }

        public void UpdatePoint(int index, Vector3? position = null, float? velocity = null)
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            if (index < 0 || index >= _points.Count) return;

            var point = _points[index];
            if (position.HasValue) point.position = position.Value;
            if (velocity.HasValue) point.targetVelocity = velocity.Value;
            _points[index] = point;
            OnPointsChanged?.Invoke();
        }

        public void DeletePoint(int index)
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            if (index < 0 || index >= _points.Count) return;
            _points.RemoveAt(index);
            OnPointDeleted?.Invoke(index);
            OnPointsChanged?.Invoke();

            if (_selectedIndex == index)
                SelectPoint(-1);
            else if (_selectedIndex > index)
                SelectPoint(_selectedIndex - 1);
        }

        public void ClearAllPoints()
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            _points.Clear();
            _selectedIndex = -1;
            OnPointsChanged?.Invoke();
            OnPointSelected?.Invoke(-1);
        }

        public void SelectPoint(int index)
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            _selectedIndex = index;
            OnPointSelected?.Invoke(index);
        }

        public void MovePointUp(int index)
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            if (index <= 0 || index >= _points.Count) return;
            var temp = _points[index];
            _points[index] = _points[index - 1];
            _points[index - 1] = temp;
            OnPointsChanged?.Invoke();
            SelectPoint(index - 1);
        }

        public void MovePointDown(int index)
        {
            if (!IsVisualActive())
            {
                Debug.LogWarning("Visual component not active!");
                return;
            }

            if (index < 0 || index >= _points.Count - 1) return;
            var temp = _points[index];
            _points[index] = _points[index + 1];
            _points[index + 1] = temp;
            OnPointsChanged?.Invoke();
            SelectPoint(index + 1);
        }

        public List<TrajectoryPoint> GetMission() => new List<TrajectoryPoint>(_points);

        public bool IsVisualActive()
        {
            if (_visualComponent == null) return true;

            return _visualComponent.isOpened();
        }
    }
}