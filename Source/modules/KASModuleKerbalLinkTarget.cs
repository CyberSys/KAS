﻿// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>Module for the kerbal vessel that allows carrying the cable heads.</summary>
// Next localization ID: #kasLOC_10003.
// FIXME: adjust nodeTransform to follow the bones.
public sealed class KASModuleKerbalLinkTarget : KASModuleLinkTargetBase,
    // KAS interfaces.
    IHasContextMenu,
    // KSPDev sugar interafces.
    IHasGUI {
  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DropConnectorHintMsg= new Message<KeyboardEventType>(
      "#kasLOC_100001",
      defaultTemplate: "To drop the connector press: [<<1>>]",
      description: "A hint string, instructing what to press in order to drop the currently carried"
      + "cable connector.\nArgument <<1>> is the current key binding of type KeyboardEventType.",
      example: "To drop the connector press: [Ctrl+Y]");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> PickupConnectorHintMsg= new Message<KeyboardEventType>(
      "#kasLOC_100002",
      defaultTemplate: "[<<1>>]: Pickup connector",
      description: "A hint string, instructing what to press in order to pickup a cable connector"
      + "which is currently in range.\nArgument <<1>> is the current key binding of type"
      + "KeyboardEventType.",
      example: "[Y]: Pickup connector");
  #endregion

  #region Part's config fields
  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string dropConnectorKey = "Y";

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string pickupConnectorKey = "Y";
  #endregion

  #region Local fields and properties
  /// <summary>Returns a valid source of the closest connector in range.</summary>
  /// <value>The source or <c>null</c>.</value>
  ILinkSource closestConnectorSource {
    get {
      return connectorsInRange
          .Select(x => x == null ? null : x.ownerModule as ILinkSource)
          .Where(x => x != null && x.linkState == LinkState.Available && x.cfgLinkType == linkType)
          .OrderBy(x => Vector3.Distance(gameObject.transform.position,
                                         x.part.gameObject.transform.position))
          .FirstOrDefault();
    }
  }

  /// <summary>List of all the connectors that triggered the interaction collision event.</summary>
  /// <remarks>
  /// This collection must be a list since the items in it can become <c>null</c> in case of Unity
  /// has destroyed the owner object.
  /// </remarks>
  readonly List<InternalKASModulePhysicalConnector> connectorsInRange =
      new List<InternalKASModulePhysicalConnector>();

  static Event dropConnectorKeyEvent;
  static Event pickupConnectorKeyEvent;
  ScreenMessage persistentTopCenterMessage;
  ScreenMessage persistentBottomCenterMessage;
  
  bool canPickupConnector {
    get {
      return !isLinked && closestConnectorSource != null;
    }
  }

  bool canDropConnector {
    get { return isLinked; }
  }
  #endregion

  #region Context menu events/actions
  /// <summary>A context menu item that picks up the cable connector in range.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_10000",
      defaultTemplate = "Pickup connector",
      description = "A context menu item that picks up the cable connector in range.")]
  public void PickupConnectorEvent() {
    var closestSource = closestConnectorSource;
    if (closestSource != null) {
      HostedDebugLog.Info(
          this, "Try picking up a physical connector of: {0}...", closestSource as PartModule);
      if (closestSource.CheckCanLinkTo(this, reportToGUI: true)
          && closestSource.StartLinking(GUILinkMode.API, LinkActorType.Player)) {
        if (!closestSource.LinkToTarget(this)) {
          closestSource.CancelLinking(LinkActorType.API);
        }
      } else {
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      }
    }
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    // Remove hints if any.
    var thisVesselIsActive = FlightGlobals.ActiveVessel == vessel;
    if (!canDropConnector || !thisVesselIsActive) {
      ScreenMessages.RemoveMessage(persistentTopCenterMessage);
      UpdateContextMenu();
    }
    if (!canPickupConnector || !thisVesselIsActive) {
      ScreenMessages.RemoveMessage(persistentBottomCenterMessage);
      UpdateContextMenu();
    }
    if (!thisVesselIsActive) {
      return;  // No GUI for the inactive vessel.
    }

    // Handle the cable head drop/pickup events. 
    if (Event.current.Equals(dropConnectorKeyEvent) && canDropConnector) {
      Event.current.Use();
      DropConnector();
    }
    if (Event.current.Equals(pickupConnectorKeyEvent) && canPickupConnector) {
      Event.current.Use();
      PickupConnectorEvent();
    }

    // Show the head drop hint message.
    if (canDropConnector) {
      persistentTopCenterMessage.message = DropConnectorHintMsg.Format(dropConnectorKeyEvent);
      ScreenMessages.PostScreenMessage(persistentTopCenterMessage);
    }
    if (canPickupConnector) {
      persistentBottomCenterMessage.message = PickupConnectorHintMsg.Format(pickupConnectorKeyEvent);
      ScreenMessages.PostScreenMessage(persistentBottomCenterMessage);
    }

    UpdateContextMenu();
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(this, PickupConnectorEvent, x => x.guiActive = canPickupConnector);
  }
  #endregion

  #region ParModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    dropConnectorKeyEvent = Event.KeyboardEvent(dropConnectorKey);
    pickupConnectorKeyEvent = Event.KeyboardEvent(pickupConnectorKey);
    useGUILayout = false;
    persistentTopCenterMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_CENTER);
    persistentBottomCenterMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.LOWER_CENTER);
    UpdateContextMenu();
  }
  #endregion

  #region KASModuleLinkTargetBase overrides
  /// <inheritdoc/>
  protected override void OnStateChange(LinkState? oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }
  #endregion

  #region Local utility methods
  void OnTriggerEnter(Collider other) {
    if (other.name == InternalKASModulePhysicalConnector.InteractionAreaCollider) {
      connectorsInRange.Add(
          other.gameObject.GetComponentInParent<InternalKASModulePhysicalConnector>());
    }
  }

  void OnTriggerExit(Collider other) {
    if (other.name == InternalKASModulePhysicalConnector.InteractionAreaCollider) {
      connectorsInRange.Remove(
          other.gameObject.GetComponentInParent<InternalKASModulePhysicalConnector>());
    }
  }

  void DropConnector() {
    linkSource.BreakCurrentLink(
        LinkActorType.Player,
        moveFocusOnTarget: linkSource.linkTarget.part.vessel == FlightGlobals.ActiveVessel);
  }
  #endregion
}

}  // namespace
