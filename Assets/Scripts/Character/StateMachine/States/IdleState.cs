using Character.Intent;
using Character.Motor;


namespace Character.StateMachine.States
{
public sealed class IdleState : ICharacterState
{
    private readonly CharacterStateMachine _fsm;
    private readonly CharacterMotor _motor;

    private MoveState _moveState;
    private SprintState _sprintState;

    public CharacterStateId Id => CharacterStateId.Idle;

    public IdleState(CharacterStateMachine fsm, CharacterMotor motor)
    {
        _fsm = fsm;
        _motor = motor;
    }

    public void SetTransitions(MoveState moveState, SprintState sprintState)
    {
        _moveState = moveState;
        _sprintState = sprintState;
    }

    public void Enter(){}

    public void Tick(CharacterIntent intent, float deltaTime){
        _motor.SetSprintActive(false);
        _motor.Tick(intent, deltaTime);

        bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
        if(!hasMove) return;

        if(intent.IsSprintHeld){
            _fsm.ChangeState(_sprintState);
            return;
        }

        _fsm.ChangeState(_moveState);
    }

    public void Exit(){}
}
}