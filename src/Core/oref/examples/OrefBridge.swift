import Foundation

/// Swift bridge for the oref Rust library
///
/// This class provides Swift-friendly wrappers around the C FFI functions
/// exported by the oref Rust library. All JSON serialization is handled
/// automatically.
///
/// Usage:
/// ```swift
/// let bridge = OrefBridge()
/// if let iob = try? bridge.calculateIOB(profile: profile, treatments: treatments) {
///     print("Current IOB: \(iob.iob)")
/// }
/// ```
public class OrefBridge {

    // MARK: - Initialization

    public init() {}

    // MARK: - Version Info

    /// Get the oref library version
    public static var version: String {
        guard let ptr = oref_version() else { return "unknown" }
        defer { oref_free_string(ptr) }
        return String(cString: ptr)
    }

    /// Perform a health check on the library
    public static func healthCheck() -> [String: Any]? {
        guard let ptr = oref_health_check() else { return nil }
        defer { oref_free_string(ptr) }

        let json = String(cString: ptr)
        guard let data = json.data(using: .utf8),
              let result = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }
        return result
    }

    // MARK: - IOB Calculation

    /// Calculate Insulin on Board from treatment history
    ///
    /// - Parameters:
    ///   - profile: User profile settings
    ///   - treatments: Array of treatments (boluses, temp basals)
    ///   - time: Time for calculation (default: now)
    ///   - currentOnly: If true, only calculate current IOB (faster)
    /// - Returns: IOB data or nil on error
    public func calculateIOB(
        profile: OrefProfile,
        treatments: [OrefTreatment],
        time: Date = Date(),
        currentOnly: Bool = true
    ) throws -> [IOBData] {
        let profileJson = try encodeToJSON(profile)
        let treatmentsJson = try encodeToJSON(treatments)
        let timeMillis = Int64(time.timeIntervalSince1970 * 1000)

        guard let resultPtr = oref_calculate_iob(
            profileJson,
            treatmentsJson,
            timeMillis,
            currentOnly ? 1 : 0
        ) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    // MARK: - COB Calculation

    /// Calculate Carbs on Board from glucose and treatment history
    public func calculateCOB(
        profile: OrefProfile,
        glucose: [GlucoseReading],
        treatments: [OrefTreatment],
        time: Date = Date()
    ) throws -> COBResult {
        let profileJson = try encodeToJSON(profile)
        let glucoseJson = try encodeToJSON(glucose)
        let treatmentsJson = try encodeToJSON(treatments)
        let timeMillis = Int64(time.timeIntervalSince1970 * 1000)

        guard let resultPtr = oref_calculate_cob(
            profileJson,
            glucoseJson,
            treatmentsJson,
            timeMillis
        ) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    // MARK: - Autosens

    /// Calculate autosens ratio from glucose history
    public func calculateAutosens(
        profile: OrefProfile,
        glucose: [GlucoseReading],
        treatments: [OrefTreatment],
        time: Date = Date()
    ) throws -> AutosensData {
        let profileJson = try encodeToJSON(profile)
        let glucoseJson = try encodeToJSON(glucose)
        let treatmentsJson = try encodeToJSON(treatments)
        let timeMillis = Int64(time.timeIntervalSince1970 * 1000)

        guard let resultPtr = oref_calculate_autosens(
            profileJson,
            glucoseJson,
            treatmentsJson,
            timeMillis
        ) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    // MARK: - Determine Basal

    /// Run the main determine-basal algorithm
    public func determineBasal(inputs: DetermineBasalInputs) throws -> DetermineBasalResult {
        let inputsJson = try encodeToJSON(inputs)

        guard let resultPtr = oref_determine_basal(inputsJson) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    /// Convenience function to run determine-basal with individual parameters
    public func determineBasalSimple(
        profile: OrefProfile,
        glucoseStatus: GlucoseStatus,
        iobData: IOBData,
        currentTemp: CurrentTemp,
        autosensRatio: Double = 1.0,
        mealCob: Double = 0.0,
        microBolusAllowed: Bool = false
    ) throws -> DetermineBasalResult {
        let profileJson = try encodeToJSON(profile)
        let glucoseStatusJson = try encodeToJSON(glucoseStatus)
        let iobDataJson = try encodeToJSON(iobData)
        let currentTempJson = try encodeToJSON(currentTemp)

        guard let resultPtr = oref_determine_basal_simple(
            profileJson,
            glucoseStatusJson,
            iobDataJson,
            currentTempJson,
            autosensRatio,
            mealCob,
            microBolusAllowed ? 1 : 0
        ) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    // MARK: - Glucose Status

    /// Calculate glucose status from readings
    public func calculateGlucoseStatus(glucose: [GlucoseReading]) throws -> GlucoseStatus {
        let glucoseJson = try encodeToJSON(glucose)

        guard let resultPtr = oref_calculate_glucose_status(glucoseJson) else {
            throw OrefError.nullPointer
        }
        defer { oref_free_string(resultPtr) }

        let resultJson = String(cString: resultPtr)
        return try decodeFromJSON(resultJson)
    }

    // MARK: - Private Helpers

    private func encodeToJSON<T: Encodable>(_ value: T) throws -> String {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let data = try encoder.encode(value)
        guard let json = String(data: data, encoding: .utf8) else {
            throw OrefError.encodingFailed
        }
        return json
    }

    private func decodeFromJSON<T: Decodable>(_ json: String) throws -> T {
        // Check for error response
        if json.contains("\"error\"") {
            if let data = json.data(using: .utf8),
               let errorDict = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let errorMessage = errorDict["error"] as? String {
                throw OrefError.algorithmError(errorMessage)
            }
        }

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        guard let data = json.data(using: .utf8) else {
            throw OrefError.decodingFailed
        }
        return try decoder.decode(T.self, from: data)
    }
}

// MARK: - Error Types

public enum OrefError: Error, LocalizedError {
    case nullPointer
    case encodingFailed
    case decodingFailed
    case algorithmError(String)

    public var errorDescription: String? {
        switch self {
        case .nullPointer:
            return "Null pointer returned from oref function"
        case .encodingFailed:
            return "Failed to encode input to JSON"
        case .decodingFailed:
            return "Failed to decode result from JSON"
        case .algorithmError(let message):
            return "Algorithm error: \(message)"
        }
    }
}

// MARK: - Data Types (matching Rust types)

public struct OrefProfile: Codable {
    public var dia: Double
    public var currentBasal: Double
    public var maxIob: Double
    public var maxDailyBasal: Double
    public var maxBasal: Double
    public var minBg: Double
    public var maxBg: Double
    public var sens: Double
    public var carbRatio: Double
    public var curve: String?
    public var peak: UInt32?

    // SMB settings
    public var enableSmbAlways: Bool?
    public var enableSmbWithCob: Bool?
    public var enableSmbWithTemptarget: Bool?
    public var enableSmbAfterCarbs: Bool?
    public var maxSmbBasalMinutes: UInt32?
    public var maxUamSmbBasalMinutes: UInt32?
    public var smbInterval: UInt32?
    public var bolusIncrement: Double?
    public var smbDeliveryRatio: Double?

    // UAM settings
    public var enableUam: Bool?

    public init(
        dia: Double,
        currentBasal: Double,
        maxIob: Double,
        maxDailyBasal: Double,
        maxBasal: Double,
        minBg: Double,
        maxBg: Double,
        sens: Double,
        carbRatio: Double
    ) {
        self.dia = dia
        self.currentBasal = currentBasal
        self.maxIob = maxIob
        self.maxDailyBasal = maxDailyBasal
        self.maxBasal = maxBasal
        self.minBg = minBg
        self.maxBg = maxBg
        self.sens = sens
        self.carbRatio = carbRatio
    }
}

public struct OrefTreatment: Codable {
    public var date: Int64
    public var timestamp: String?
    public var insulin: Double?
    public var carbs: Double?
    public var rate: Double?
    public var duration: Double?
    public var eventType: String?

    public init(date: Int64 = 0, timestamp: String? = nil, insulin: Double? = nil,
                carbs: Double? = nil, rate: Double? = nil, duration: Double? = nil,
                eventType: String? = nil) {
        self.date = date
        self.timestamp = timestamp
        self.insulin = insulin
        self.carbs = carbs
        self.rate = rate
        self.duration = duration
        self.eventType = eventType
    }

    public static func bolus(_ insulin: Double, at date: Date) -> OrefTreatment {
        OrefTreatment(
            date: Int64(date.timeIntervalSince1970 * 1000),
            timestamp: ISO8601DateFormatter().string(from: date),
            insulin: insulin,
            eventType: "Bolus"
        )
    }

    public static func carbs(_ grams: Double, at date: Date) -> OrefTreatment {
        OrefTreatment(
            date: Int64(date.timeIntervalSince1970 * 1000),
            timestamp: ISO8601DateFormatter().string(from: date),
            carbs: grams,
            eventType: "Carbs"
        )
    }
}

public struct GlucoseReading: Codable {
    public var glucose: Double
    public var date: Int64
    public var dateString: String?
    public var noise: Double?
    public var direction: String?

    public init(glucose: Double, date: Date) {
        self.glucose = glucose
        self.date = Int64(date.timeIntervalSince1970 * 1000)
    }
}

public struct GlucoseStatus: Codable {
    public var glucose: Double
    public var delta: Double
    public var shortAvgdelta: Double
    public var longAvgdelta: Double
    public var date: Int64
    public var noise: Double?
}

public struct IOBData: Codable {
    public var iob: Double
    public var activity: Double
    public var basalIob: Double
    public var bolusIob: Double
    public var netBasalInsulin: Double
    public var bolusInsulin: Double
    public var time: Int64
    public var lastBolusTime: Int64?
}

public struct COBResult: Codable {
    public var mealCob: Double
    public var carbsAbsorbed: Double
    public var currentDeviation: Double
    public var maxDeviation: Double
    public var minDeviation: Double
    public var slopeFromMax: Double
    public var slopeFromMin: Double
}

public struct AutosensData: Codable {
    public var ratio: Double
}

public struct CurrentTemp: Codable {
    public var duration: Double
    public var rate: Double
    public var temp: String

    public init(duration: Double = 0, rate: Double = 0, temp: String = "absolute") {
        self.duration = duration
        self.rate = rate
        self.temp = temp
    }

    public static var none: CurrentTemp {
        CurrentTemp(duration: 0, rate: 0, temp: "absolute")
    }
}

public struct MealData: Codable {
    public var carbs: Double
    public var mealCob: Double

    public init(carbs: Double = 0, mealCob: Double = 0) {
        self.carbs = carbs
        self.mealCob = mealCob
    }
}

public struct DetermineBasalInputs: Codable {
    public var glucoseStatus: GlucoseStatus
    public var currentTemp: CurrentTemp
    public var iobData: IOBData
    public var profile: OrefProfile
    public var autosensData: AutosensData
    public var mealData: MealData
    public var microBolusAllowed: Bool
    public var currentTimeMillis: Int64?
}

public struct DetermineBasalResult: Codable {
    public var rate: Double?
    public var duration: UInt32?
    public var reason: String
    public var cob: Double
    public var iob: Double
    public var eventualBg: Double
    public var insulinReq: Double?
    public var units: Double?
    public var tick: String?
    public var error: String?
    public var sensitivityRatio: Double?
    public var variableSens: Double?
    public var predictedBg: [Double]?
    public var predBgsUam: [Double]?
    public var predBgsIob: [Double]?
    public var predBgsZt: [Double]?
    public var predBgsCob: [Double]?
    public var bgMinsAgo: Double?
    public var targetBg: Double?
    public var smbEnabled: Bool?
    public var carbsReq: Double?
    public var threshold: Double?

    /// Check if an SMB is recommended
    public var hasSMB: Bool {
        guard let units = units else { return false }
        return units > 0
    }

    /// Check if a temp basal change is recommended
    public var hasTemp: Bool {
        rate != nil && duration != nil
    }

    /// Check if there was an error
    public var hasError: Bool {
        error != nil
    }
}
