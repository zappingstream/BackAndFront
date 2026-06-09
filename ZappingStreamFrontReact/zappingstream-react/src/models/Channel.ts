export interface UpcomingVideo {
  VideoId: string;
  Title: string;
  ThumbnailUrl: string;
  IsPremiere: boolean;
  PublishedAt?: string;
  ScheduledStartTime?: string;
  ActualStartTime?: string;
  ActualEndTime?: string;
  AddedAt?: string;

  Live?: boolean;
}

export interface ActiveVideo {
  VideoId: string;
  Title: string;
  ThumbnailUrl: string;
  IsPremiere: boolean;
  PublishedAt?: string;
  ScheduledStartTime?: string;
  ActualStartTime?: string;
  ActualEndTime?: string;
  AddedAt?: string;

  Live?: boolean;
}

export interface PastVideo {
  VideoId: string;
  Title: string;
  ThumbnailUrl: string;
  WasPremiere: boolean;

  PublishedAt?: string;
  ScheduledStartTime?: string;
  ActualStartTime?: string;
  ActualEndTime?: string;
  EndedAt?: string;
}

export interface Channel {
  ChannelName: string;

  LastActivityAt: string;
  ChannelDescription?: string;
  ChannelCity?: string;
  ChannelType?: string;
  ChannelLiveUrl?: string;
  ChannelImgUrl?: string;
  ChannelBannerUrl?: string;
  Upcoming?: Record<string, UpcomingVideo>;
  Actives?: Record<string, ActiveVideo>;
  Past?: Record<string, PastVideo>;

}