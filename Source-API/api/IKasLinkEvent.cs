﻿// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv2 {

/// <summary>A holder for simple source-to-target event.</summary>
public interface IKasLinkEvent {
  /// <summary>Link source.</summary>
  /// <value>The link source module.</value>
  ILinkSource source { get; }

  /// <summary>Link target.</summary>
  /// <value>The link target module.</value>
  ILinkTarget target { get; }

  /// <summary>Actor who changed the links tate.</summary>
  /// <value>The actor type that initiated the action.</value>
  LinkActorType actor { get; }
}

}  // namespace
