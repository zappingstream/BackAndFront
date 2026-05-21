export interface UpcomingVideo {
  VideoId: string;
  Title: string;
  ScheduledStartTime: string;
  ThumbnailUrl: string;
  AddedAt: string;
  Live: boolean;
  IsPremiere: boolean;
}

export interface ActiveVideo {
  VideoId: string;
  Title: string;
  ScheduledStartTime: string;
  ThumbnailUrl: string;
  AddedAt: string;
  Live: boolean;
  IsPremiere: boolean;
}

export interface Channel {
  ChannelName: string;
  ChannelDescription: string;
  ChannelCity: string;
  ChannelType: string;
  ChannelLiveUrl: string;
  ChannelBannerUrl: string;
  ChannelImgUrl: string;
  LastActivityAt: string; 

  ChannelLive: boolean;
  ChannelImgLiveUrl: string;
  LiveVideoId: string;
  IsPremiere: boolean;

  Upcoming: Record<string, UpcomingVideo>;
  Actives: Record<string, ActiveVideo>;
}