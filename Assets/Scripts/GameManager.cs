using UnityEngine;

namespace Seeman
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameState State { get; private set; } = GameState.Booting;

        [HideInInspector] public FishController Fish;
        [HideInInspector] public ConversationManager Conversation;
        [HideInInspector] public PersonalitySystem Personality;
        [HideInInspector] public LocalAIClient AI;
        [HideInInspector] public GameUI UI;
        [HideInInspector] public CRTController CRT;
        [HideInInspector] public NarratorSystem Narrator;
        [HideInInspector] public FeedingSystem Feeding;
        [HideInInspector] public MicrophoneRecorder Mic;

        public Bounds TankBounds = new Bounds(new Vector3(0f, 0.55f, 0.25f), new Vector3(2.2f, 0.85f, 1.0f));

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Fish = FindAnyObjectByType<FishController>();
            Conversation = FindAnyObjectByType<ConversationManager>();
            Personality = FindAnyObjectByType<PersonalitySystem>();
            AI = FindAnyObjectByType<LocalAIClient>();
            UI = FindAnyObjectByType<GameUI>();
            CRT = FindAnyObjectByType<CRTController>();
            Narrator = FindAnyObjectByType<NarratorSystem>();
            Feeding = FindAnyObjectByType<FeedingSystem>();
            Mic = FindAnyObjectByType<MicrophoneRecorder>();
        }

        void Start()
        {
            SetState(GameState.Idle);
        }

        public void SetState(GameState s)
        {
            State = s;
            if (UI != null) UI.OnStateChanged(s);
        }
    }
}
