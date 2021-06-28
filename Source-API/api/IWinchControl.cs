﻿// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv2 {

/// <summary>Interface that allows operating the winch parts.</summary>
public interface IWinchControl : ILinkSource {
  /// <summary>Maximum speed of retracting or extending the cable.</summary>
  /// <value>Speed in meters per second.</value>
  /// <seealso cref="motorTargetSpeed"/>
  /// <seealso cref="SetMotor"/>
  float cfgMotorMaxSpeed { get; }

  /// <summary>Maximum reserve of the cable in the winch.</summary>
  /// <remarks>
  /// This is the maximum possible distance between the winch and its connector head. 
  /// </remarks>
  /// <value>The length of the cable in meters.</value>
  float cfgMaxCableLength { get; }

  /// <summary>Tells if the cable connector head is locked into the winch.</summary>
  /// <remarks>
  /// In the locked state there is no free cable available, and there is no moving part
  /// (the connector). If the winch is linked to a part and the connector is locked, then the target
  /// part is <i>docked</i> to the vessel that owns the winch. When the connector unlocks, the
  /// attached part <i>undocks</i> from the vessel.
  /// </remarks>
  /// <value><c>true</c> if the connector is rigid relative to the owner part.</value>
  /// <seealso cref="ILinkPeer.isLinked"/>
  /// <seealso cref="ILinkSource.linkJoint"/>
  /// <seealso cref="ILinkJoint.coupleOnLinkMode"/>
  /// <seealso cref="SetMotor"/>
  bool isConnectorLocked { get; }
  
  /// <summary>Amount of the cable that was extended till the moment.</summary>
  /// <remarks>
  /// This value is dynamic and can be affected by the motor. This is <i>not</i> the actual distance
  /// between the winch and the connector head! In order to find one, take the
  /// <c>physicalAnchorTransform</c> values from the source and target, and calculate the
  /// distance between their positions.
  /// </remarks>
  /// <value>The length of the cable in meters.</value>
  /// <seealso cref="SetMotor"/>
  /// <seealso cref="StretchCable"/>
  /// <seealso cref="ReleaseCable"/>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkTarget"/>
  float currentCableLength { get; }

  /// <summary>Current speed of the motor spindle.</summary>
  /// <remarks>
  /// <p>
  /// This is the speed at which the cable is being extended or retracted at the current moment.
  /// The actual speed of the motor can differ from what was set via the control methods (e.g.
  /// <see cref="SetMotor"/>) due to there is some inertia momentum. Negative speed means the cable
  /// is being retracted, and the positive speed means the cable is being extended.
  /// </p>
  /// <p>
  /// The motor speed is always trying to match the <see cref="motorTargetSpeed"/>. Depending on the
  /// part's implementation and settings, some time may be needed to actually have the match.
  /// </p>
  /// </remarks>
  /// <value>The speed in meters per second.</value>
  /// <seealso cref="SetMotor"/>
  /// <seealso cref="motorTargetSpeed"/>
  float motorCurrentSpeed { get; }

  /// <summary>Desired speed of the motor spindle.</summary>
  /// <remarks>
  /// Ideally, the motor is always working at this speed. However, in the physics world of KSP the
  /// motor may operate at the lower or the higher speeds. It depends of the various conditions.
  /// </remarks>
  /// <value>The speed target. It's can never exceed the part's limits setting.</value>
  /// <seealso cref="motorCurrentSpeed"/>
  /// <seealso cref="cfgMotorMaxSpeed"/>
  /// <seealso cref="SetMotor"/>
  float motorTargetSpeed { get; }

  /// <summary>Sets the winch motor to the desired speed.</summary>
  /// <remarks>
  /// <p>
  /// The motor is responsible for the deployed cable length changing. It can extend the cable,
  /// retract the cable, or do nothing (idle). The winch and its head cannot get separated at a
  /// greater distance than the current deployed cable length. That said, the motor is controlling
  /// the distance.
  /// </p>
  /// <p>
  /// The motor speed is not required to change immediately. The motor may need some time to get to
  /// the target speed. It depends on the part implementation and configuration. The rule of thumb
  /// is to not expect the <see cref="motorCurrentSpeed"/> to match the
  /// <paramref name="targetSpeed"/> right after the method call. There may be some time needed
  /// before the values will match. However, the <see cref="motorTargetSpeed"/> value will change
  /// immediately, and will match the parameter. 
  /// </p>
  /// <p>
  /// Setting the motor speed may affect the connector state. E.g. if the connector was locked,
  /// and the motor speed is set to a positive value (extending), then the connector is get
  /// deployed.
  /// </p>
  /// <p>
  /// The motor will automatically stop when the cable length reaches zero or the maximum allowed
  /// value. In case of the zero length, the connector will be attempted to lock into the winch.
  /// This attempt may fail due to the bad align of the connector. To retry the attempt, just call
  /// this method again with a negative value.
  /// </p>
  /// </remarks>
  /// <param name="targetSpeed">
  /// The new speed of the motor. The <i>positive</i> value instructs to extend the cable, and the
  /// <i>negative</i> value commands to retract the cable. Zero value turns the motor off. The
  /// infinite values can be used to set the target speed to the maximum allowed speed on the part.
  /// </param>
  /// <seealso cref="motorTargetSpeed"/>
  /// <seealso cref="motorCurrentSpeed"/>
  /// <seealso cref="isConnectorLocked"/>
  /// <seealso cref="cfgMaxCableLength"/>
  /// <seealso cref="currentCableLength"/>
  /// <seealso cref="StretchCable"/>
  /// <seealso cref="ReleaseCable"/>
  void SetMotor(float targetSpeed);
  
  /// <summary>
  /// Sets the deployed cable length to the actual distance between the winch and the connector.
  /// </summary>
  /// <remarks>This will "stretch" the cable by reducing the unused cable.</remarks>
  /// <seealso cref="currentCableLength"/>
  /// <seealso cref="SetMotor"/>
  /// <seealso cref="ReleaseCable"/>
  void StretchCable();

  /// <summary>Sets the deployed cable length to the maximum value allowed by the part.</summary>
  /// <remarks>If the connector is locked, then it will be deployed.</remarks>
  /// <seealso cref="cfgMaxCableLength"/>
  /// <seealso cref="isConnectorLocked"/>
  /// <seealso cref="SetMotor"/>
  /// <seealso cref="StretchCable"/>
  void ReleaseCable();
}

}  // namespace
