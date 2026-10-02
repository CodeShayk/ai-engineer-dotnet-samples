namespace Ch05.StructuredPatterns;

/// <summary>Messages, emails and supplier text used by the sample. Order numbers match the seed data.</summary>
public static class SampleInputs
{
    public static readonly string[] ClassificationMessages =
    [
        "The jacket is lovely but it's too small. Could I swap it for a large?",
        "Tracking says delivered but nothing came. I just want my money back.",
        "Does the warranty cover the battery on the Aurora earbuds?",
        "The boots are fine, I suppose, but my partner hates them. Not sure what to do."
    ];

    public static readonly InboundEmail[] Emails =
    [
        new("E-1001", """
            Hi, I'd like to return the Summit Hiking Boots from order NW-10254. They rub on my heel
            even after a week of wearing them in. Can I get my money back? Thanks, Christina Berglund
            """),
        new("E-1002", """
            The Brewmaster kettle I bought from you has started leaking from the base.
            I'd like a replacement please.
            Maria Anders
            """),
        new("E-1003", """
            order NW-10262 - daypack straps are already fraying after two walks. can u repair or swap it?
            """),
        new("E-1004", """
            Before I buy the Orbit watch: if I open it and don't like it, can I still return it?
            """),
        new("E-1005", """
            Returning both Lumen lamps from NW-10255, the light is much colder than in the photos.
            Refund to my card please. Hanna Moos
            """),
        new("E-1006", """
            My Nimbus speaker (NW-10252) crackles at any volume above half. Can you fix it or send a
            new one? Best wishes, Ana Trujillo
            """),
        new("E-1007", """
            I want to send back the thing I bought last month.
            """),
        new("E-1008", """
            Hello, the rain jacket from order NW-10260 isn't as waterproof as described - I got soaked
            on the first wet day. I would like a refund. Regards, Hanna Moos
            """)
    ];

    public static readonly string[] SupplierTexts =
    [
        """
        BRAND NEW!!! Amazing quality Thermo-Steel Insulated Travel Mug 450ml - keeps drinks HOT 6 hrs /
        COLD 12 hrs!!! leakproof flip lid, fits most car cup holders, BPA free. weight 0.7 lb.
        Hand wash only - do NOT microwave. Best mug ever, customers love it!!
        """,
        """
        Explorer LED Head Torch. 300 lumens. 3 modes (high / low / red). USB-C rechargeable, approx
        5 hours on high. IPX4 splash resistant. Adjustable elastic strap. Weight 85g.
        Do not look directly into the beam.
        """
    ];
}
